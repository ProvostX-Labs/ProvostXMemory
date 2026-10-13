using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Consolidation;
using ProvostXMemory.Eval;
using ProvostXMemory.Fusion;
using ProvostXMemory.VectorIndex;

using Xunit;

namespace ProvostXMemory.SystemTests.Journey;

/// <summary>
/// THE knowledge-assistant journey, as a host would experience it: seed a scope with the
/// deterministic eval corpus (40+ synthetic docs), run the golden question set, assert
/// Recall@5 ≥ 0.9 through the eval harness, consolidate, re-run (score must not regress),
/// restart the service over the same root, re-assert — every step on the public
/// IMemoryService surface only.
/// </summary>
public sealed class KnowledgeAssistantJourneyTests : IDisposable
{
    private const double MinimumRecallAt5 = 0.9;
    private const int TopK = 5;

    private readonly TempRoot _root = new("journey-knowledge");

    public void Dispose() => _root.Dispose();

    private static (DeterministicEvalCorpus Corpus, EvalCaseSet Cases, MemoryScope Scope) Journey()
    {
        // 40 docs in one scope + two additional golden cases over sub-cluster terms → 10 questions.
        var corpus = new DeterministicEvalCorpus(seed: 2026, scopeCount: 1, recordCountPerScope: 40);
        var scope = corpus.Scopes[0];
        var baseCases = corpus.BuildEvalCaseSet();
        var extra = new List<EvalCase>
        {
            new("jq-extra-kubernetes", "kubernetes helm chart release", scope, corpus.RecordIdsOf(scope, 0)),
            new("jq-extra-billing", "invoice chargeback receipt", scope, corpus.RecordIdsOf(scope, 3)),
        };
        var cases = new EvalCaseSet("knowledge-journey", baseCases.Cases.Concat(extra).ToList());
        return (corpus, cases, scope);
    }

    private static async Task<MemoryService> LoadCorpusAsync(string root, DeterministicEvalCorpus corpus, MemoryScope scope)
    {
        var service = SystemRig.BuildService(root);
        foreach (var corpusRecord in corpus.Records)
        {
            await service.RememberAsync(corpusRecord.Record);
        }
        await service.BuildIndexesAsync(scope);
        return service;
    }

    private static async Task<double> RecallAt5Async(EvalRunner runner, IMemoryService service, EvalCaseSet cases, long total)
    {
        var report = await runner.EvaluateAsync(service, cases, new EvalRunnerOptions(topK: TopK), total);
        return report.Aggregate.RecallAtK;
    }

    private sealed class ThrowingFactory : EvalServiceFactory
    {
        public static readonly ThrowingFactory Instance = new();

        public IMemoryService Create(string rootPath) =>
            throw new InvalidOperationException("Journey must not build new services via EvalRunner.");
    }

    [Fact]
    public async Task Seed_index_evaluate_consolidate_restart_holds_recall()
    {
        var (corpus, cases, scope) = Journey();
        var runner = new EvalRunner(ThrowingFactory.Instance);

        // ---- Phase 1: seed + index + golden questions ----
        var service = await LoadCorpusAsync(_root.Path_, corpus, scope);
        var firstScore = await RecallAt5Async(runner, service, cases, corpus.TotalRecords);
        Assert.True(firstScore >= MinimumRecallAt5, $"initial recall@5 {firstScore} below {MinimumRecallAt5}");

        // ---- Phase 2: consolidation pass (dedup + TTL; no summaries so top-5 stays intact) ----
        // Inject a near-duplicate pair (dedup material) and an expired record (TTL material).
        var duplicated = new MemoryRecord("journey-dup-old", scope, "journey-dup-old", "zqkvwvjm bxqzzzqk filler token block")
        {
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            Tags = new[] { "journey" },
        };
        var duplicate = new MemoryRecord("journey-dup-new", scope, "journey-dup-new", "zqkvwvjm bxqzzzqk filler token block")
        {
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            Tags = new[] { "journey-extra" },
        };
        var expired = new MemoryRecord("journey-ttl", scope, "journey-ttl", "mjqkvpxwbm zzzyqqforx2 filler token block")
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        await service.RememberAsync(duplicated);
        await service.RememberAsync(duplicate);
        await service.RememberAsync(expired);
        await service.BuildIndexesAsync(scope);

        await service.ConsolidateAsync(scope, new ConsolidationOptions(DedupSimilarityThreshold: 0.99999, ExpireTtl: true, EmitClusterSummaries: false));

        // Consolidation effects, exactly: TTL gone, newer dup gone, survivor tagged with the union.
        Assert.Null(await service.GetAsync(scope, "journey-ttl"));
        Assert.Null(await service.GetAsync(scope, "journey-dup-new"));
        var survivor = await service.GetAsync(scope, "journey-dup-old");
        Assert.NotNull(survivor);
        Assert.Contains("journey", survivor.Tags);
        Assert.Contains("journey-extra", survivor.Tags);

        // ---- Phase 3: re-run questions — must NOT regress ----
        var postScore = await RecallAt5Async(runner, service, cases, corpus.TotalRecords);
        Assert.True(postScore >= MinimumRecallAt5, $"post-consolidation recall@5 {postScore} below {MinimumRecallAt5}");
        Assert.True(postScore >= firstScore - 1e-9, $"consolidation regressed recall@5: {firstScore} → {postScore}");

        // The duplicate's content still recalls — via the survivor.
        var dupQuery = await service.RecallAsync(new RetrievalQuery("zqkvwvjm bxqzzzqk filler token block", scope, 5));
        Assert.DoesNotContain(dupQuery.Records, hit => hit.Record.Id == "journey-dup-new");
        Assert.Contains(dupQuery.Records, hit => hit.Record.Id == "journey-dup-old");

        // ---- Phase 4: full restart over the same root, re-assert ----
        var restarted = SystemRig.AttachService(_root.Path_);
        Assert.Contains(restarted.Scopes(), enumerated => enumerated.Kind == scope.Kind && enumerated.Id == scope.Id);
        Assert.Null(await restarted.GetAsync(scope, "journey-ttl")); // TTL deletion persisted

        await restarted.BuildIndexesAsync(scope);
        var restartScore = await RecallAt5Async(runner, restarted, cases, corpus.TotalRecords);
        Assert.True(restartScore >= MinimumRecallAt5, $"post-restart recall@5 {restartScore} below {MinimumRecallAt5}");
        Assert.Equal(firstScore, restartScore); // identical to the pre-restart golden score

        // Consolidated state persisted: duplicate still gone in the fresh instance.
        Assert.Null(await restarted.GetAsync(scope, "journey-dup-new"));
        Assert.NotNull(await restarted.GetAsync(scope, "journey-dup-old"));
    }
}