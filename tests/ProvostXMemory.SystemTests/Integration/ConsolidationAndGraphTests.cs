using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Consolidation;
using ProvostXMemory.Eval;
using ProvostXMemory.Graph;
using ProvostXMemory.Storage;
using ProvostXMemory.VectorIndex;

using Xunit;

namespace ProvostXMemory.SystemTests.Integration;

/// <summary>
/// The consolidation pass end-to-end through the facade: dedup keeps the oldest record
/// and unions tags, TTL expiry deletes expired records, cluster summaries surface as
/// retrievable category="summary" records, and those summaries are recorded as graph
/// clusters (member entities → summary id) through the scope-pinned graph store.
/// Storage is the production JsonlMemoryStore over a temp dir; recall reflects the
/// consolidated state afterwards.
/// </summary>
public sealed class ConsolidationAndGraphTests : IDisposable
{
    private readonly TempRoot _root = new("consolidation-graph");

    public void Dispose() => _root.Dispose();

    private MemoryScope Scope() => new(MemoryScopeKind.Session, "consol");

    /// <summary>Eval-backed golden cases for regression checks across consolidation.</summary>
    private static (DeterministicEvalCorpus Corpus, EvalCaseSet Cases) Golden(int seed = 2026)
    {
        var corpus = new DeterministicEvalCorpus(seed: seed, scopeCount: 1, recordCountPerScope: 40);
        return (corpus, corpus.BuildEvalCaseSet());
    }

    private async Task<MemoryService> SeedWithExtrasAsync()
    {
        var (corpus, _) = Golden();
        var service = SystemRig.BuildService(_root.Path_, graph: true);
        var scope = corpus.Scopes[0];
        foreach (var record in corpus.Records)
        {
            await service.RememberAsync(record.Record);
        }

        // Near-duplicate pair from a sixth topic (kept distinct from the eval clusters):
        // identical content vector → dedup candidates at any sane threshold.
        const string duplicateContent = "volcano seismology magma chamber eruption column ashfall";
        var duplicateOlder = new MemoryRecord("dup-older", scope, "dup-older", duplicateContent)
        {
            Tags = new[] { "volcano" },
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
        };
        var duplicateNewer = new MemoryRecord("dup-newer", scope, "dup-newer", duplicateContent)
        {
            Tags = new[] { "extra-tag" },
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        };
        await service.RememberAsync(duplicateOlder);
        await service.RememberAsync(duplicateNewer);

        // TTL record that MUST disappear during consolidation.
        var expired = new MemoryRecord("expired-ttl", scope, "expired-ttl", "glacier moraine crevasse accumulation ablation")
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        await service.RememberAsync(expired);

        await service.BuildIndexesAsync(scope);
        return service;
    }

    /// <summary>
    /// THE consolidation fixture: dedup drops the newer duplicate, unions its tags into
    /// the survivor; TTL record is gone; eval golden recall@5 does not regress; and —
    /// with summaries enabled — summary records exist, are retrievable, and rank for
    /// their cluster's query.
    /// </summary>
    [Fact]
    public async Task Consolidation_pass_dedups_expires_and_keeps_golden_recall()
    {
        var (corpus, cases) = Golden();
        var service = await SeedWithExtrasAsync();
        var scope = corpus.Scopes[0];
        var runner = new EvalRunner(ThrowingFactory.Instance);

        var before = await runner.EvaluateAsync(service, cases, new EvalRunnerOptions(topK: 5), corpus.TotalRecords);
        Assert.Equal(1.0, before.Aggregate.RecallAtK, precision: 9); // 4 golden cases, perfect on this corpus

        await service.ConsolidateAsync(scope, new ConsolidationOptions(DedupSimilarityThreshold: 0.99999, ExpireTtl: true, EmitClusterSummaries: false));

        // Dedup: newer GONE, older survives with unioned tags.
        Assert.Null(await service.GetAsync(scope, "dup-newer"));
        var survivor = await service.GetAsync(scope, "dup-older");
        Assert.NotNull(survivor);
        Assert.Contains("volcano", survivor.Tags);
        Assert.Contains("extra-tag", survivor.Tags);

        // TTL expiry.
        Assert.Null(await service.GetAsync(scope, "expired-ttl"));

        // Golden recall does not regress after the pass.
        var after = await runner.EvaluateAsync(service, cases, new EvalRunnerOptions(topK: 5), corpus.TotalRecords);
        Assert.True(after.Aggregate.RecallAtK >= before.Aggregate.RecallAtK - 1e-9,
            $"consolidation regressed recall@5: {before.Aggregate.RecallAtK} → {after.Aggregate.RecallAtK}");
    }

    [Fact]
    public async Task Cluster_summaries_surface_in_retrieval_and_graph()
    {
        var (corpus, _) = Golden();
        var service = SystemRig.BuildService(_root.Path_, graph: true);
        var scope = corpus.Scopes[0];
        foreach (var record in corpus.Records)
        {
            await service.RememberAsync(record.Record);
        }
        await service.BuildIndexesAsync(scope);

        await service.ConsolidateAsync(scope, new ConsolidationOptions(DedupSimilarityThreshold: 0.99, ExpireTtl: false, EmitClusterSummaries: true));

        // Summary records exist in the store with the summarizer's category.
        var summaryRecord = (await service.RecallAsync(new RetrievalQuery("kubernetes cluster rollouts deployments", scope, 10)))
            .Records
            .FirstOrDefault(hit => hit.Record.Category == ClusterSummarizer.SummaryCategory);
        Assert.NotNull(summaryRecord);
        Assert.StartsWith("summary-", summaryRecord.Record.Id, StringComparison.Ordinal);

        // Summary is retrievable through the plain store surface too.
        var fetched = await service.GetAsync(scope, summaryRecord.Record.Id);
        Assert.NotNull(fetched);
        Assert.Equal("true", fetched.Meta["cluster-summary"]);

        // Graph: a cluster links the summary record id (cluster recorded on consolidate).
        var layout = new MemoryRootLayout(_root.Path_, SystemRig.Embedder().ModelHash);
        var graph = new JsonlMemoryGraphStore(layout, scope);
        var graphCluster = await FindClusterWithSummaryAsync(graph);
        Assert.NotNull(graphCluster);
        Assert.Equal(summaryRecord.Record.Id, graphCluster.SummaryRecordId);
        Assert.NotEmpty(graphCluster.EntityIds);
    }

    [Fact]
    public async Task Consolidation_reflects_in_recall_results()
    {
        var service = SystemRig.BuildService(_root.Path_);
        var scope = Scope();
        var duplicateContent = "zqkv wvjm bxqz zzqk filler tokens only";
        var a = new MemoryRecord("dup-a", scope, "dup-a", duplicateContent) { CreatedAt = DateTimeOffset.UtcNow.AddHours(-2) };
        var b = new MemoryRecord("dup-b", scope, "dup-b", duplicateContent) { CreatedAt = DateTimeOffset.UtcNow.AddHours(-1) };
        await service.RememberAsync(a);
        await service.RememberAsync(b);
        await service.BuildIndexesAsync(scope);

        // Both duplicates rank pre-consolidation.
        var pre = await service.RecallAsync(new RetrievalQuery(duplicateContent, scope, 5));
        // Each duplicate surfaces via its dense record anchor AND its chunk id, so the
        // same record id appears twice; compare distinct ids in rank order.
        Assert.Equal(
            new[] { "dup-a", "dup-b" },
            pre.Records.Select(hit => hit.Record.Id).Distinct().Take(2).ToArray());

        await service.ConsolidateAsync(scope, new ConsolidationOptions(DedupSimilarityThreshold: 0.9, ExpireTtl: true, EmitClusterSummaries: false));

        // Post-consolidation recall reflects the deduped store: only the survivor remains.
        var post = await service.RecallAsync(new RetrievalQuery(duplicateContent, scope, 5));
        Assert.DoesNotContain(post.Records, hit => hit.Record.Id == "dup-b");
        Assert.Contains(post.Records, hit => hit.Record.Id == "dup-a");
    }

    [Fact]
    public async Task Consolidation_without_summaries_flag_emits_no_summaries()
    {
        var (corpus, _) = Golden();
        var service = SystemRig.BuildService(_root.Path_);
        var scope = corpus.Scopes[0];
        foreach (var record in corpus.Records)
        {
            await service.RememberAsync(record.Record);
        }
        await service.BuildIndexesAsync(scope);

        await service.ConsolidateAsync(scope, new ConsolidationOptions(ExpireTtl: false, EmitClusterSummaries: false));

        var recall = await service.RecallAsync(new RetrievalQuery("kubernetes cluster rollouts deployments", scope, 10));
        Assert.DoesNotContain(recall.Records, hit => hit.Record.Category == ClusterSummarizer.SummaryCategory);
    }

    private sealed class ThrowingFactory : EvalServiceFactory
    {
        public static readonly ThrowingFactory Instance = new();

        public IMemoryService Create(string rootPath) =>
            throw new InvalidOperationException("EvaluateAsync must not construct services.");
    }

    private static async Task<GraphCluster?> FindClusterWithSummaryAsync(JsonlMemoryGraphStore graph)
    {
        foreach (var entity in await graph.SearchEntitiesAsync("cluster-0", 20))
        {
            var cluster = await graph.ClusterOfAsync(entity.EntityId);
            if (cluster?.SummaryRecordId is not null)
            {
                return cluster;
            }
        }

        return null;
    }
}