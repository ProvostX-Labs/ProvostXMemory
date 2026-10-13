using ProvostXMemory.Abstractions;
using ProvostXMemory.Consolidation;
using ProvostXMemory.Eval;
using ProvostXMemory.Fusion;
using ProvostXMemory.Graph;
using ProvostXMemory.Logging;
using ProvostXMemory.Storage;
using ProvostXMemory.Telemetry;
using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;
using ProvostXMemory.VectorIndex.Gpu;

namespace ProvostXMemory.Examples;

/// <summary>
/// Shared wiring helper for the examples: every service is composed exactly as a host
/// would — <see cref="MemoryServiceBuilder"/> over the production <see cref="JsonlMemoryStore"/>,
/// the deterministic <see cref="ClusterHashEmbedder"/> (no real model, no network), and the
/// own-engine backends. All storage roots live under the temp directory; nothing personal
/// and nothing outside the example's own root is touched.
/// </summary>
public sealed class ExampleRig
{
    /// <summary>Vector dimension used by every example (small keeps runs fast).</summary>
    public const int Dim = 128;

    /// <summary>Creates a fresh unique root directory under the temp path.</summary>
    public string FreshRoot(string tag)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "provostxmemory-examples",
            $"{tag}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Deterministic, cluster-preserving fake embedder (same text → same vector).</summary>
    public ClusterHashEmbedder CreateEmbedder() => new(Dim);

    /// <summary>
    /// Builds a <see cref="MemoryService"/> the production way. All parameters optional so
    /// each example specializes exactly one aspect at a time.
    /// </summary>
    public MemoryService BuildService(
        string root,
        ClusterHashEmbedder? embedder = null,
        IndexKind indexKind = IndexKind.Flat,
        IVectorIndexFactory? indexFactory = null,
        ConsolidationOptions? consolidation = null,
        bool graph = false,
        MemoryLoggingOptions? logging = null,
        Correction.IRetrievalGrader? grader = null)
    {
        var embedderOwned = embedder ?? CreateEmbedder();
        var layout = new MemoryRootLayout(root, embedderOwned.ModelHash);
        var builder = new MemoryServiceBuilder()
            .WithOptions(new MemoryServiceOptions(
                Storage: new StorageOptions(root),
                Vector: new VectorOptions(embedderOwned.ModelHash, embedderOwned.Dim, indexKind),
                Graph: GraphOptions.Default,
                Fusion: new FusionOptions(),
                Consolidation: consolidation ?? new ConsolidationOptions(),
                Logging: logging))
            .WithStore(new JsonlMemoryStore(layout))
            .WithEmbedder(embedderOwned)
            .WithIndexFactory(indexFactory ?? new EngineVectorIndexFactory())
            .WithFuser(new ReciprocalRankFuser())
            .WithTelemetry(NoopMemoryTelemetry.Instance);
        if (graph)
        {
            builder
                .WithGraphExtractor(new HeuristicEntityExtractor())
                .WithGraphStoreResolver(scope => new JsonlMemoryGraphStore(layout, scope));
        }

        if (grader is not null)
        {
            builder.WithGrader(grader);
        }

        return builder.Build();
    }

    /// <summary>IVF factory with compact deterministic settings for small corpora.</summary>
    public EngineVectorIndexFactory IvfFactory() =>
        new(new EngineVectorIndexFactory.Options(
            new FlatExactIndex.Options(),
            new IvfFlatIndex.Options(nlist: 4, nprobe: 4, kmeansIterations: 12, seed: 2026),
            HnswOptions.Default));

    /// <summary>HNSW factory with compact deterministic settings.</summary>
    public EngineVectorIndexFactory HnswFactory() =>
        new(new EngineVectorIndexFactory.Options(
            new FlatExactIndex.Options(),
            new IvfFlatIndex.Options(),
            new HnswOptions(
                M: 8,
                MaxLevel0Connections: 16,
                LevelFactor: 0.65,
                EfConstruction: 120,
                EfSearch: 96,
                Seed: 1729)));

    /// <summary>
    /// FlatJson factory: one <see cref="VectorNamespaceFile"/> per created scope index,
    /// rooted under the example root for realistic restart behavior.
    /// </summary>
    public sealed class FlatJsonFactory : IVectorIndexFactory
    {
        private readonly string _dir;
        private readonly string _modelHash;
        private int _created;

        public FlatJsonFactory(string root, string modelHash)
        {
            _dir = Path.Combine(root, "examples-vector-index");
            Directory.CreateDirectory(_dir);
            _modelHash = modelHash;
        }

        public IVectorIndex Create(IndexKind kind, int dim)
        {
            _created++;
            var file = new VectorNamespaceFile(
                Path.Combine(_dir, $"idx-{_created}.json"), _modelHash, dim);
            return new FlatJsonVectorIndex(file, new VectorIndexGuard(_modelHash, dim));
        }
    }
}
