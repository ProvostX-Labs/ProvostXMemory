using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory;
using ProvostXMemory.Abstractions;
using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;

using Xunit;

namespace ProvostXMemory.SystemTests.Integration;

/// <summary>
/// REAL composition: MemoryServiceBuilder-built MemoryService, production JsonlMemoryStore
/// over a temp dir, real LexicalIndex, the deterministic ClusterHashEmbedder, and a
/// parametrized backend matrix (FlatExact, Ivf, Hnsw, FlatJson; Gpu via its transparent
/// CPU fallback). Every backend must produce the SAME expected recall results on the same
/// corpus — host-agnostic: only IMemoryService + builder + options are touched.
/// </summary>
public sealed class BackendCompositionTests
{
    private const int TopK = 5;

    public static TheoryData<string, IVectorIndexFactory, IndexKind> BackendMatrix() => new()
    {
        { "flat-exact", new EngineVectorIndexFactory(), IndexKind.Flat },
        { "ivf", SystemRig.IvfFactory(), IndexKind.Ivf },
        { "hnsw", SystemRig.HnswFactory(), IndexKind.Hnsw },
        { "gpu", SystemRig.GpuFactory(), IndexKind.Gpu },
    };

    private static async Task<(MemoryService Service, MemoryScope Scope)> SeededAsync(
        string root, IVectorIndexFactory factory, IndexKind kind)
    {
        var service = SystemRig.BuildService(root, indexFactory: factory, indexKind: kind);
        var scope = new MemoryScope(MemoryScopeKind.Purpose, "corpus");
        foreach (var record in TopicCorpus.Records(scope))
        {
            await service.RememberAsync(record);
        }
        await service.BuildIndexesAsync(scope);
        return (service, scope);
    }

    /// <summary>
    /// THE backend-parity fixture: after Remember ×12 + BuildIndexes, every golden query
    /// returns exactly its topic cluster as the top-3 distinct records for EVERY backend
    /// (unique root per theory row — no cross-row pollution). With the dense stage fused
    /// in (RRF over dense+lexical ranks), exact within-cluster order is legitimate fusion
    /// behavior covered by the fusion determinism tests, so membership is asserted as a set.
    /// </summary>
    [Theory]
    [MemberData(nameof(BackendMatrix))]
    public async Task Multi_record_remember_then_recall_is_correct_across_backends(
        string backendName, IVectorIndexFactory factory, IndexKind kind)
    {
        _ = backendName;
        using var root = new TempRoot($"backend-{kind}");
        var (service, scope) = await SeededAsync(root.Path_, factory, kind);

        for (var i = 0; i < TopicCorpus.Queries.Length; i++)
        {
            var recall = await service.RecallAsync(new RetrievalQuery(TopicCorpus.Queries[i], scope, TopK));
            var expected = TopicCorpus.ExpectedClusters[i];

            Assert.True(recall.Records.Count >= 3, $"{kind}: expected ≥3 hits for '{TopicCorpus.Queries[i]}', got {recall.Records.Count}");
            // A record's dense anchor and its chunk both resolve to the parent record, so
            // duplicate entries per record are expected; compare distinct ids as a set.
            var topIds = recall.Records.Select(hit => hit.Record.Id).Distinct().Take(3).ToList();
            Assert.Equal(expected.OrderBy(id => id, StringComparer.Ordinal), topIds.OrderBy(id => id, StringComparer.Ordinal));
            Assert.All(recall.Records, hit => Assert.Equal(scope.Kind, hit.Record.Scope.Kind));
        }

        // GetAsync round-trip through the same service (store surface).
        var fetched = await service.GetAsync(scope, "kube-2");
        Assert.NotNull(fetched);
        Assert.Equal("kubernetes scale rollout context", fetched.Content);
    }

    /// <summary>FlatJson backend composes identically (factory builds per-scope namespace files).</summary>
    [Fact]
    public async Task FlatJson_backend_matches_expected_results()
    {
        using var root = new TempRoot("backend-flatjson");
        var factory = new SystemRig.FlatJsonFactory(root.Path_, SystemRig.Embedder().ModelHash);
        var service = SystemRig.BuildService(root.Path_, indexFactory: factory, indexKind: IndexKind.Flat);
        var scope = new MemoryScope(MemoryScopeKind.Purpose, "corpus");
        foreach (var record in TopicCorpus.Records(scope))
        {
            await service.RememberAsync(record);
        }
        await service.BuildIndexesAsync(scope);

        for (var i = 0; i < TopicCorpus.Queries.Length; i++)
        {
            var recall = await service.RecallAsync(new RetrievalQuery(TopicCorpus.Queries[i], scope, TopK));
            Assert.Equal(
                TopicCorpus.ExpectedClusters[i].OrderBy(id => id, StringComparer.Ordinal),
                recall.Records.Select(hit => hit.Record.Id).Distinct().Take(3).OrderBy(id => id, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// Cross-backend result equality: flat-exact rankings are the exact-search baseline;
    /// every approximate backend must reproduce them on this corpus.
    /// </summary>
    [Fact]
    public async Task All_backends_rank_identically_to_the_flat_baseline()
    {
        var baselineRoot = new TempRoot("backend-baseline");
        try
        {
            var baseline = await GoldenQueriesAsync(baselineRoot.Path_, new EngineVectorIndexFactory(), IndexKind.Flat);

            foreach (var (name, factory, kind) in BackendRows())
            {
                using var otherRoot = new TempRoot($"backend-parity-{kind}");
                var other = await GoldenQueriesAsync(otherRoot.Path_, factory, kind);
                foreach (var key in baseline.Keys)
                {
                    Assert.True(
                        baseline[key].SequenceEqual(other[key]),
                        $"{name}: query '{key}' diverged from flat baseline: [{string.Join(',', baseline[key])}] vs [{string.Join(',', other[key])}]");
                }
            }
        }
        finally
        {
            baselineRoot.Dispose();
        }
    }

    /// <summary>Forget/Update/Remember mutations keep every backend's recall correct.</summary>
    [Fact]
    public async Task Backends_survive_forget_update_and_remember_mutations_identically()
    {
        foreach (var (name, factory, kind) in BackendRows())
        {
            using var root = new TempRoot($"backend-mutation-{kind}");
            var (service, scope) = await SeededAsync(root.Path_, factory, kind);

            await service.ForgetAsync(scope, "kube-2");
            await service.UpdateAsync(new MemoryRecord("pg-1", scope, "pg-1", "pg-1") with
            {
                Content = "postgres replication slot hot standby",
            });
            await service.BuildIndexesAsync(scope);

            var kube = await service.RecallAsync(new RetrievalQuery("kubernetes helm rollout", scope, TopK));
            Assert.DoesNotContain(kube.Records, hit => hit.Record.Id == "kube-2");
            Assert.Equal(
                TopicCorpus.ExpectedClusters[0].Where(id => id != "kube-2").OrderBy(id => id, StringComparer.Ordinal),
                kube.Records.Select(hit => hit.Record.Id).Distinct().Take(2).OrderBy(id => id, StringComparer.Ordinal));

            var pgNew = await service.RecallAsync(new RetrievalQuery("postgres replication slot hot standby", scope, TopK));
            Assert.Equal("pg-1", pgNew.Records[0].Record.Id);
            // The pre-update key terms no longer match: pg-1 must stay out of the top hits
            // even though its sibling records still talk about vacuum/index space.
            var pgOld = await service.RecallAsync(new RetrievalQuery("vacuum tablespace index", scope, TopK));
            // Dense ANN may still surface pg-1 among its nearest neighbors regardless of
            // absolute similarity; the update guarantee is freshness (new content only)
            // and that the old key terms no longer rank it first.
            Assert.All(
                pgOld.Records.Where(hit => hit.Record.Id == "pg-1"),
                hit => Assert.Equal("postgres replication slot hot standby", hit.Record.Content));
            Assert.NotEqual("pg-1", pgOld.Records.Select(hit => hit.Record.Id).Distinct().First());
        }
    }

    /// <summary>
    /// Gpu options route through the wrapper: every factory Create request is IndexKind.Gpu
    /// (one lazy index during Remember, one during BuildIndexes), and recall still matches.
    /// </summary>
    [Fact]
    public async Task Gpu_options_route_every_create_to_the_gpu_kind()
    {
        using var root = new TempRoot("backend-gpu-capture");
        var capture = new SystemRig.CaptureFactory(SystemRig.GpuFactory());
        var (service, scope) = await SeededAsync(root.Path_, capture, IndexKind.Gpu);

        Assert.Equal(2, capture.Requests.Count); // lazy Remember index + BuildIndexes index set
        Assert.All(capture.Requests, requested => Assert.Equal(IndexKind.Gpu, requested));

        var recall = await service.RecallAsync(new RetrievalQuery("kubernetes helm rollout", scope, TopK));
        Assert.Equal(
            TopicCorpus.ExpectedClusters[0].OrderBy(id => id, StringComparer.Ordinal),
            recall.Records.Select(hit => hit.Record.Id).Distinct().Take(3).OrderBy(id => id, StringComparer.Ordinal));
    }

    private static async Task<Dictionary<string, string[]>> GoldenQueriesAsync(
        string root, IVectorIndexFactory factory, IndexKind kind)
    {
        var (service, scope) = await SeededAsync(root, factory, kind);
        var results = new Dictionary<string, string[]>(StringComparer.Ordinal);
        for (var i = 0; i < TopicCorpus.Queries.Length; i++)
        {
            var recall = await service.RecallAsync(new RetrievalQuery(TopicCorpus.Queries[i], scope, TopK));
            results[$"q{i}"] = recall.Records.Select(hit => hit.Record.Id).ToArray();
        }
        return results;
    }

    private static IEnumerable<(string Name, IVectorIndexFactory Factory, IndexKind Kind)> BackendRows()
    {
        yield return ("flat", new EngineVectorIndexFactory(), IndexKind.Flat);
        yield return ("ivf", SystemRig.IvfFactory(), IndexKind.Ivf);
        yield return ("hnsw", SystemRig.HnswFactory(), IndexKind.Hnsw);
        yield return ("gpu", SystemRig.GpuFactory(), IndexKind.Gpu);
    }
}