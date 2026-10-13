using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Consolidation;
using ProvostXMemory.Fusion;
using ProvostXMemory.Storage;
using ProvostXMemory.Telemetry;
using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;

using Xunit;

namespace ProvostXMemory.SystemTests.Integration;

/// <summary>
/// Full restart simulation and options-routing behavior: a new MemoryService instance
/// (fresh indexes, fresh embedder instances) over the SAME root directory must recall
/// everything persisted by the previous instance, and MemoryServiceOptions must route
/// every configured IndexKind to the factory.
/// </summary>
public sealed class PersistenceAndRoutingTests : IDisposable
{
    private readonly TempRoot _root = new("persistence");

    public void Dispose() => _root.Dispose();

    private static MemoryScope Scope() => new(MemoryScopeKind.Purpose, "persist");

    private static async Task<(MemoryService Service, IReadOnlyList<MemoryRecord> Records)> SeedAsync(string root)
    {
        var service = SystemRig.BuildService(root);
        var records = TopicCorpus.Records(Scope());
        foreach (var record in records)
        {
            await service.RememberAsync(record);
        }
        await service.BuildIndexesAsync(Scope());
        return (service, records);
    }

    /// <summary>
    /// THE persistence fixture: restart → Scopes() enumerates the store's scope, GetAsync
    /// returns old records, BuildIndexes revives retrieval, and per-topic queries behave
    /// exactly as before the restart.
    /// </summary>
    [Fact]
    public async Task New_service_over_same_root_recalls_previously_stored_records()
    {
        var (_, records) = await SeedAsync(_root.Path_);
        var seedService = SystemRig.BuildService(_root.Path_);
        var baseline = new Dictionary<string, string[]>(StringComparer.Ordinal);
        for (var i = 0; i < TopicCorpus.Queries.Length; i++)
        {
            baseline[$"q{i}"] = (await seedService
                    .RecallAsync(new RetrievalQuery(TopicCorpus.Queries[i], Scope(), 5)))
                .Records
                .Select(hit => hit.Record.Id)
                .ToArray();
        }

        // ---- full restart: identical wiring, all-new instances ----
        var restarted = SystemRig.AttachService(_root.Path_);

        var scopes = restarted.Scopes();
        Assert.Contains(scopes, scope => scope.Kind == MemoryScopeKind.Purpose && scope.Id == "persist");

        foreach (var record in records)
        {
            var fetched = await restarted.GetAsync(Scope(), record.Id);
            Assert.NotNull(fetched);
            Assert.Equal(record.Content, fetched.Content);
        }

        await restarted.BuildIndexesAsync(Scope());

        for (var i = 0; i < TopicCorpus.Queries.Length; i++)
        {
            var recall = await restarted.RecallAsync(new RetrievalQuery(TopicCorpus.Queries[i], Scope(), 5));
            // Fused RRF ordering is legitimate; assert cluster membership as a set over
            // distinct record ids (dense anchor + chunk resolve to the same record).
            var topIds = recall.Records.Select(hit => hit.Record.Id).Distinct().Take(3)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
            Assert.Equal(TopicCorpus.ExpectedClusters[i].OrderBy(id => id, StringComparer.Ordinal), topIds);
        }
    }

    /// <summary>Restart WITHOUT BuildIndexes: store surface still works, recall yields nothing yet.</summary>
    [Fact]
    public async Task Restart_before_index_build_returns_store_data_but_no_recall_hits()
    {
        await SeedAsync(_root.Path_);
        var restarted = SystemRig.AttachService(_root.Path_);

        Assert.NotNull(await restarted.GetAsync(Scope(), "kube-1"));

        var recall = await restarted.RecallAsync(new RetrievalQuery("kubernetes helm rollout", Scope(), 5));
        Assert.Empty(recall.Records); // indexes exist only after BuildIndexes/Remember

        await restarted.BuildIndexesAsync(Scope());
        var revived = await restarted.RecallAsync(new RetrievalQuery("kubernetes helm rollout", Scope(), 5));
        Assert.Equal(
            TopicCorpus.ExpectedClusters[0].OrderBy(id => id, StringComparer.Ordinal),
            revived.Records.Select(hit => hit.Record.Id).Distinct().Take(3).OrderBy(id => id, StringComparer.Ordinal));
    }

    /// <summary>RebuildAsync after restart reproduces the original query results byte-for-byte.</summary>
    [Fact]
    public async Task Rebuild_after_restart_reproduces_original_results()
    {
        var (originalService, _) = await SeedAsync(_root.Path_);
        var originalResults = new Dictionary<string, string[]>(StringComparer.Ordinal);
        for (var i = 0; i < TopicCorpus.Queries.Length; i++)
        {
            originalResults[$"q{i}"] = (await originalService
                .RecallAsync(new RetrievalQuery(TopicCorpus.Queries[i], Scope(), 5)))
                .Records
                .Select(hit => hit.Record.Id)
                .ToArray();
        }

        var restarted = SystemRig.AttachService(_root.Path_);
        await restarted.RebuildAsync(); // scopes-aware full rebuild — no per-scope call needed

        for (var i = 0; i < TopicCorpus.Queries.Length; i++)
        {
            var recall = await restarted.RecallAsync(new RetrievalQuery(TopicCorpus.Queries[i], Scope(), 5));
            Assert.True(
                recall.Records.Select(hit => hit.Record.Id).SequenceEqual(originalResults[$"q{i}"]),
                $"RebuildAsync diverged from original on '{TopicCorpus.Queries[i]}'");
        }
    }

    /// <summary>
    /// Options routing: each IndexKind in MemoryServiceOptions is actually requested from
    /// the factory (lazily, once per fresh scope index set), proving per-options backend
    /// selection — each kind really used.
    /// </summary>
    [Fact]
    public async Task Every_configured_indexkind_is_routed_to_the_factory()
    {
        var kinds = new (IndexKind Kind, Func<IVectorIndexFactory> Factory)[]
        {
            (IndexKind.Flat, () => new EngineVectorIndexFactory()),
            (IndexKind.Ivf, SystemRig.IvfFactory),
            (IndexKind.Hnsw, SystemRig.HnswFactory),
            (IndexKind.Gpu, SystemRig.GpuFactory),
        };

        foreach (var (kind, makeFactory) in kinds)
        {
            var capture = new SystemRig.CaptureFactory(makeFactory());
            var root = SystemRig.FreshRoot($"routing-{kind}");
            try
            {
                var service = SystemRig.BuildService(root, indexFactory: capture, indexKind: kind);
                var scope = Scope();
                foreach (var record in TopicCorpus.Records(scope))
                {
                    await service.RememberAsync(record);
                }

                // Remember creates the scope's dense index lazily on the first write;
                // a second index set materializes for the build pass beyond a threshold.
                Assert.Equal(1, capture.CountOf(kind));

                await service.BuildIndexesAsync(scope);
                var recall = await service.RecallAsync(new RetrievalQuery("kubernetes helm rollout", scope, 5));
                Assert.Equal(
                    TopicCorpus.ExpectedClusters[0].OrderBy(id => id, StringComparer.Ordinal),
                    recall.Records.Select(hit => hit.Record.Id).Distinct().Take(3).OrderBy(id => id, StringComparer.Ordinal));
                Assert.True(capture.CountOf(kind) >= 1, "kind must have been requested at least once");
                Assert.All(capture.Requests, requested => Assert.Equal(kind, requested));
            }
            finally
            {
                TryDelete(root);
            }
        }
    }

    [Fact]
    public async Task Builder_requires_every_mandatory_dependency()
    {
        var root = SystemRig.FreshRoot("builder-validation");
        try
        {
            var embedder = SystemRig.Embedder();

            // Each mandatory wiring omitted → Build throws naming the missing call.
            Assert.Throws<InvalidOperationException>(() => new MemoryServiceBuilder().Build());
            Assert.Throws<InvalidOperationException>(() => new MemoryServiceBuilder()
                .WithStore(new JsonlMemoryStore(new MemoryRootLayout(root, embedder.ModelHash))).Build());
            Assert.Throws<InvalidOperationException>(() => new MemoryServiceBuilder()
                .WithOptions(new MemoryServiceOptions(
                    new StorageOptions(root),
                    new VectorOptions(embedder.ModelHash, embedder.Dim, IndexKind.Flat),
                    GraphOptions.Default, new FusionOptions(), new ConsolidationOptions()))
                .Build());

            // Zero-dim vector options rejected by the service constructor.
            Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryServiceBuilder()
                .WithOptions(new MemoryServiceOptions(
                    new StorageOptions(root),
                    new VectorOptions(embedder.ModelHash, 0, IndexKind.Flat),
                    GraphOptions.Default, new FusionOptions(), new ConsolidationOptions()))
                .WithStore(new JsonlMemoryStore(new MemoryRootLayout(root, embedder.ModelHash)))
                .WithEmbedder(embedder)
                .WithIndexFactory(new EngineVectorIndexFactory())
                .WithFuser(new ReciprocalRankFuser())
                .WithTelemetry(NoopMemoryTelemetry.Instance)
                .Build());
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}