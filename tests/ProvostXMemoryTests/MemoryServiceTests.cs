using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Consolidation;
using ProvostXMemory.Correction;
using ProvostXMemory.Fusion;
using ProvostXMemory.Graph;
using ProvostXMemory.Reranking;
using ProvostXMemory.Storage;
using ProvostXMemory.Telemetry;
using ProvostXMemory.VectorIndex.Engine;

using Xunit;

namespace ProvostXMemoryTests;

/// <summary>
/// MemoryService end-to-end: Remember → Recall across scopes with isolation, Forget
/// removes visibility, Update refreshes indexes, consolidation through the facade,
/// plan fan-out merge correctness, builder wiring.
/// </summary>
public sealed class MemoryServiceTests : IDisposable
{
    private const string RootDir = "pvmx-facade-tests";

    private readonly string _rootPath;

    public MemoryServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), RootDir, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_rootPath, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private MemoryServiceOptions Options() => new(
        new StorageOptions(_rootPath),
        new VectorOptions("model-hash-v1", 128),
        GraphOptions.Default,
        new FusionOptions(),
        new ConsolidationOptions());

    private (MemoryService Service, FacadeStore Store, HashEmbedder Embedder) Create(
        IRetrievalGrader? grader = null)
    {
        var store = new FacadeStore();
        var embedder = new HashEmbedder();
        var builder = new MemoryServiceBuilder()
            .WithOptions(Options())
            .WithStore(store)
            .WithEmbedder(embedder)
            .WithIndexFactory(new EngineVectorIndexFactory())
            .WithFuser(new ReciprocalRankFuser())
            .WithTelemetry(NoopMemoryTelemetry.Instance);
        if (grader is not null)
        {
            builder.WithGrader(grader);
        }

        return (builder.Build(), store, embedder);
    }

    /// <summary>Facade wiring with graph extractor + scope-pinned graph stores (cluster path).</summary>
    private (MemoryService Service, FacadeStore Store, HashEmbedder Embedder) CreateWithGraph()
    {
        var store = new FacadeStore();
        var embedder = new HashEmbedder();
        var service = (MemoryService)new MemoryServiceBuilder()
            .WithOptions(Options())
            .WithStore(store)
            .WithEmbedder(embedder)
            .WithIndexFactory(new EngineVectorIndexFactory())
            .WithFuser(new ReciprocalRankFuser())
            .WithTelemetry(NoopMemoryTelemetry.Instance)
            .WithGraphExtractor(new HeuristicEntityExtractor(new[] { "kubernetes" }))
            .WithGraphStoreResolver(scope =>
                new JsonlMemoryGraphStore(new MemoryRootLayout(_rootPath, "model-hash-v1"), scope))
            .Build();
        return (service, store, embedder);
    }

    private static MemoryRecord Rec(MemoryScope scope, string id, string content, string[]? tags = null)
        => new(
            Id: id,
            Scope: scope,
            Key: id,
            Content: content)
        {
            Tags = tags ?? Array.Empty<string>(),
        };

    /// <summary>
    /// Regression pin (2026-10-14, second audit): concurrent Remember on ONE scope used
    /// to corrupt the unsynchronized lexical index (duplicate-id throws / torn postings).
    /// All mutations serialize under the service mutation gate; embeddings stay outside.
    /// </summary>
    [Fact]
    public async Task Concurrent_Remember_on_one_scope_stays_consistent()
    {
        var (service, _, _) = Create();
        var scope = new MemoryScope(MemoryScopeKind.Session, "conc-1");

        var tasks = new Task[4];
        for (var t = 0; t < tasks.Length; t++)
        {
            var threadIndex = t;
            tasks[t] = Task.Run(async () =>
            {
                for (var i = 0; i < 10; i++)
                {
                    await service.RememberAsync(Rec(scope, $"rec-{threadIndex}-{i}", $"content number {i} of thread {threadIndex}"));
                }
            });
        }

        await Task.WhenAll(tasks);

        var result = await service.RecallAsync(new RetrievalQuery("content", scope, 100));
        var ids = result.Records.Select(hit => hit.Record.Id).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(40, ids.Count); // all 40 records visible, none lost, none duplicated

        var retrieved = service.Scopes();
        Assert.Contains(scope, retrieved);
    }

    /// <summary>
    /// Regression pin (2026-10-14, second audit): the facade used to rerank TWICE (hybrid
    /// retriever reranks internally with the same injected reranker, then the facade
    /// reranked again) — score boosts compounded. The facade must rerank exactly once.
    /// </summary>
    private sealed class CountingReranker : IReranker
    {
        public int Calls;

        public Task<IReadOnlyList<ScoredMemoryRecord>> RerankAsync(
            RetrievalQuery query,
            IReadOnlyList<ScoredMemoryRecord> candidates,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(candidates);
        }
    }

    [Fact]
    public async Task Recall_reranks_exactly_once()
    {
        var store = new FacadeStore();
        var embedder = new HashEmbedder();
        var reranker = new CountingReranker();
        var service = new MemoryServiceBuilder()
            .WithOptions(Options())
            .WithStore(store)
            .WithEmbedder(embedder)
            .WithIndexFactory(new EngineVectorIndexFactory())
            .WithFuser(new ReciprocalRankFuser())
            .WithTelemetry(NoopMemoryTelemetry.Instance)
            .WithReranker(reranker)
            .Build();
        var scope = new MemoryScope(MemoryScopeKind.Session, "rerank-1");

        await service.RememberAsync(Rec(scope, "alpha", "alpha content"));
        await service.RecallAsync(new RetrievalQuery("alpha", scope, 5));

        Assert.Equal(1, reranker.Calls); // hybrid reranks internally; the facade must not rerank again
    }

    [Fact]
    public async Task Remember_then_Recall_returns_the_record()
    {
        var (service, _, _) = Create();
        var scope = new MemoryScope(MemoryScopeKind.Session, "s1");
        await service.RememberAsync(Rec(scope, "kube-note", "kubernetes cluster deployment rollout notes"));
        await service.BuildIndexesAsync(scope);

        var result = await service.RecallAsync(new RetrievalQuery("kubernetes deployment", scope, 3));

        Assert.Contains(result.Records, hit => hit.Record.Id == "kube-note");
    }

    [Fact]
    public async Task Scope_isolation_record_in_A_invisible_to_B()
    {
        var (service, _, _) = Create();
        var scopeA = new MemoryScope(MemoryScopeKind.Tenant, "acme");
        var scopeB = new MemoryScope(MemoryScopeKind.Tenant, "globex");
        await service.RememberAsync(Rec(scopeA, "secret-1", "acme confidential salary bands"));
        await service.BuildIndexesAsync(scopeA);
        await service.BuildIndexesAsync(scopeB);

        var resultA = await service.RecallAsync(new RetrievalQuery("confidential salary", scopeA, 5));
        var resultB = await service.RecallAsync(new RetrievalQuery("confidential salary", scopeB, 5));

        Assert.Contains(resultA.Records, hit => hit.Record.Id == "secret-1");
        // Scope B sees nothing of scope A's record — structural isolation through the facade.
        Assert.DoesNotContain(resultB.Records, hit => hit.Record.Id == "secret-1");
        Assert.Empty(resultB.Records);
    }

    [Fact]
    public async Task Forget_removes_the_record_from_store_and_indexes()
    {
        var (service, store, _) = Create();
        var scope = new MemoryScope(MemoryScopeKind.Session, "s1");
        await service.RememberAsync(Rec(scope, "temp-1", "temporary memory kubernetes"));
        await service.BuildIndexesAsync(scope);

        var before = await service.RecallAsync(new RetrievalQuery("kubernetes", scope, 5));
        Assert.Contains(before.Records, hit => hit.Record.Id == "temp-1");

        await service.ForgetAsync(scope, "temp-1");

        Assert.Null(await service.GetAsync(scope, "temp-1"));
        var after = await service.RecallAsync(new RetrievalQuery("kubernetes", scope, 5));
        Assert.DoesNotContain(after.Records, hit => hit.Record.Id == "temp-1");
        Assert.Empty(after.Records);
        Assert.Equal(0, await store.CountAsync("Session", "s1"));
    }

    [Fact]
    public async Task Update_replaces_the_indexed_content()
    {
        var (service, _, _) = Create();
        var scope = new MemoryScope(MemoryScopeKind.Session, "s1");
        var record = Rec(scope, "doc-1", "original content about kubernetes");
        await service.RememberAsync(record);
        await service.BuildIndexesAsync(scope);

        var updated = record with { Content = "updated content about postgres database tuning" };
        await service.UpdateAsync(updated);

        var kubeResult = await service.RecallAsync(new RetrievalQuery("kubernetes", scope, 5));
        var pgResult = await service.RecallAsync(new RetrievalQuery("postgres tuning", scope, 5));

        // A single-record scope is always surfaced by the dense stage (ANN returns its
        // nearest neighbors regardless of absolute similarity — previously masked by the
        // dense-resolve bug). The update guarantee is freshness: no hit may carry the
        // stale pre-update content, and the new terms must retrieve doc-1.
        Assert.DoesNotContain(
            kubeResult.Records,
            hit => hit.Record.Content != "updated content about postgres database tuning");
        Assert.Contains(pgResult.Records, hit => hit.Record.Id == "doc-1");
        var stored = await service.GetAsync(scope, "doc-1");
        Assert.Equal("updated content about postgres database tuning", stored!.Content);
    }

    [Fact]
    public async Task Duplicate_remember_throws_and_is_isolated_per_scope()
    {
        var (service, _, _) = Create();
        var scope = new MemoryScope(MemoryScopeKind.Session, "s1");
        await service.RememberAsync(Rec(scope, "rec-1", "first content"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RememberAsync(Rec(scope, "rec-1", "second content")));

        // Same id in another scope is fine — scopes are isolated namespaces.
        var other = new MemoryScope(MemoryScopeKind.Session, "s2");
        await service.RememberAsync(Rec(other, "rec-1", "other scope content"));
        Assert.NotNull(await service.GetAsync(other, "rec-1"));
    }

    [Fact]
    public async Task Plan_fan_out_merges_across_scopes()
    {
        var (service, _, _) = Create();
        var root = new MemoryScope(MemoryScopeKind.Tenant, "acme");
        var child = new MemoryScope(MemoryScopeKind.Session, "acme-child", root);
        await service.RememberAsync(Rec(root, "root-doc", "annual report kubernetes rollout"));
        await service.RememberAsync(Rec(child, "child-doc", "team kubernetes offsite notes"));
        await service.BuildIndexesAsync(root);
        await service.BuildIndexesAsync(child);

        var plan = new QueryPlan("plan-1", new[]
        {
            new QueryPlanStep("step-root", "search", new[] { "Tenant/acme" }, 5),
            new QueryPlanStep("step-child", "search", new[] { "Session/acme-child" }, 5),
        });
        var result = await service.RecallAsync(new RetrievalQuery("kubernetes", root, 5) { Plan = plan });

        Assert.Contains(result.Records, hit => hit.Record.Id == "root-doc");
        Assert.Contains(result.Records, hit => hit.Record.Id == "child-doc");
        // Every returned record belongs to one of the fanned-out scopes.
        Assert.All(result.Records, hit => Assert.True(
            string.Equals(hit.Record.Scope.Id, "acme", StringComparison.Ordinal)
            || string.Equals(hit.Record.Scope.Id, "acme-child", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Consolidate_through_facade_deduplicates()
    {
        var (service, store, _) = Create();
        var scope = new MemoryScope(MemoryScopeKind.Session, "s1");
        var older = Rec(scope, "keep-me", "alpha bravo charlie delta", tags: new[] { "auth" }) with
        {
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
        };
        var newer = Rec(scope, "drop-me", "alpha bravo charlie delta", tags: new[] { "login-page" }) with
        {
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await service.RememberAsync(older);
        await service.RememberAsync(newer);

        await service.ConsolidateAsync(scope, new ConsolidationOptions());

        // Near-duplicate pair: the oldest member survives; the newer one is deleted.
        Assert.Null(await service.GetAsync(scope, "drop-me"));
        var kept = await service.GetAsync(scope, "keep-me");
        Assert.NotNull(kept);
        Assert.Equal(new[] { "auth", "login-page" }, kept!.Tags);
        Assert.Equal(1, await store.CountAsync("Session", "s1"));
    }

    [Fact]
    public async Task BuildIndexes_and_Rebuild_restore_fresh_indexes()
    {
        var (service, store, _) = Create();
        var scope = new MemoryScope(MemoryScopeKind.Session, "s1");
        await service.RememberAsync(Rec(scope, "doc-1", "kubernetes deployment notes"));

        // BuildIndexes must produce a searchable scope index from the store snapshot.
        await service.BuildIndexesAsync(scope);
        var result = await service.RecallAsync(new RetrievalQuery("kubernetes", scope, 5));
        Assert.Contains(result.Records, hit => hit.Record.Id == "doc-1");

        // Rebuild resets everything and re-derives from the store.
        await service.RebuildAsync();
        var rebuilt = await service.RecallAsync(new RetrievalQuery("kubernetes", scope, 5));
        Assert.Contains(rebuilt.Records, hit => hit.Record.Id == "doc-1");
        Assert.Equal(1, await store.CountAsync("Session", "s1"));
    }

    [Fact]
    public async Task Scopes_lists_all_seeded_scopes()
    {
        var (service, _, _) = Create();
        var scopeA = new MemoryScope(MemoryScopeKind.Session, "s1");
        var scopeB = new MemoryScope(MemoryScopeKind.Tenant, "acme");
        await service.RememberAsync(Rec(scopeA, "rec-1", "content A"));
        await service.RememberAsync(Rec(scopeB, "rec-2", "content B"));

        var scopes = service.Scopes();

        Assert.Equal(2, scopes.Count);
        Assert.Contains(scopes, scope => scope.Kind == MemoryScopeKind.Session && scope.Id == "s1");
        Assert.Contains(scopes, scope => scope.Kind == MemoryScopeKind.Tenant && scope.Id == "acme");
    }

    [Fact]
    public async Task Builder_missing_wiring_throws()
    {
        var builder = new MemoryServiceBuilder()
            .WithOptions(Options())
            .WithStore(new FacadeStore())
            .WithEmbedder(new HashEmbedder());

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("WithIndexFactory", ex.Message, StringComparison.Ordinal);
        Assert.Contains("WithFuser", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Corrective_recall_routes_through_grader_when_wired()
    {
        // Grader grades everything relevant → corrective retriever keeps the hybrid result.
        var service = Create(new RelevantGrader()).Service;
        var scope = new MemoryScope(MemoryScopeKind.Session, "s1");
        await service.RememberAsync(Rec(scope, "doc-1", "kubernetes rollout notes"));
        await service.BuildIndexesAsync(scope);

        var result = await service.RecallAsync(new RetrievalQuery("kubernetes", scope, 5));

        Assert.NotEmpty(result.Records);
        // The corrective pipeline's graded output carries its original (hybrid) provenance;
        // the facade routing is observable through the graded diagnostics instead.
        Assert.Contains(result.Diagnostics, stage => stage.Stage == "correction.graded");
    }

    // Test-only grader: grades every candidate relevant.
    private sealed class RelevantGrader : IRetrievalGrader
    {
        public Task<GradedCandidate> GradeAsync(RetrievalQuery query, ScoredMemoryRecord candidate, CancellationToken cancellationToken = default)
            => Task.FromResult(new GradedCandidate(candidate, RetrievalGrade.Relevant));
    }

    [Fact]
    public async Task Consolidation_summaries_are_recorded_as_graph_clusters()
    {
        var (service, store, _) = CreateWithGraph();

        var scope = new MemoryScope(MemoryScopeKind.Session, "s1");
        // HashEmbedder (verified collision-free slots): 7 shared tokens + 1 own token per
        // record → cos ≈ 0.875: below dedup 0.92 (survivors kept) and above cluster-join
        // 0.86 (clustered into one summary of 3 members).
        await service.RememberAsync(Rec(scope, "kub-1", "kubernetes cluster rollout helm chart release ledger notes"));
        await service.RememberAsync(Rec(scope, "kub-2", "kubernetes cluster rollout helm chart release spindle notes"));
        await service.RememberAsync(Rec(scope, "kub-3", "kubernetes cluster rollout helm chart release quartz notes"));
        await service.BuildIndexesAsync(scope);

        await service.ConsolidateAsync(scope, new ConsolidationOptions(EmitClusterSummaries: true));

        var storeRecords = await store.ListAsync("Session", "s1");
        var summary = storeRecords.Single(record => record.Category == ClusterSummarizer.SummaryCategory);
        Assert.Equal("true", summary.Meta["cluster-summary"]);

        var graph = new JsonlMemoryGraphStore(new MemoryRootLayout(_rootPath, "model-hash-v1"), scope);
        var clusterOfMember = await graph.ClusterOfAsync("kubernetes");
        Assert.NotNull(clusterOfMember);
        Assert.Equal(summary.Id, clusterOfMember!.SummaryRecordId);
        Assert.Contains("kubernetes", clusterOfMember.EntityIds);
    }

    [Fact]
    public async Task Cluster_recording_is_off_when_no_graph_store_is_wired()
    {
        var (service, store, _) = Create();
        var scope = new MemoryScope(MemoryScopeKind.Session, "s1");
        await service.RememberAsync(Rec(scope, "kub-1", "kubernetes cluster rollout helm chart release ledger notes"));
        await service.RememberAsync(Rec(scope, "kub-2", "kubernetes cluster rollout helm chart release spindle notes"));
        await service.RememberAsync(Rec(scope, "kub-3", "kubernetes cluster rollout helm chart release quartz notes"));
        await service.BuildIndexesAsync(scope);

        await service.ConsolidateAsync(scope, new ConsolidationOptions(EmitClusterSummaries: true));

        // The summary record exists, but no clusters.jsonl appears anywhere in the root —
        // consolidations without graph wiring stay byte-identical to the pre-cluster behavior.
        Assert.Contains(
            await store.ListAsync("Session", "s1"),
            record => record.Category == ClusterSummarizer.SummaryCategory);
        Assert.False(Directory.Exists(Path.Combine(_rootPath, "Session", "s1", "graph")));
    }

}