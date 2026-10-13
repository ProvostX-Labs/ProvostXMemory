using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ProvostXMemory;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Consolidation;
using ProvostXMemory.Eval;
using ProvostXMemory.Fusion;
using ProvostXMemory.Graph;
using ProvostXMemory.Storage;
using ProvostXMemory.Telemetry;
using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;
using ProvostXMemory.VectorIndex.Gpu;

using Xunit;

namespace ProvostXMemory.SystemTests;

/// <summary>
/// Shared wiring for the agnostic system tests: every service here is composed exactly as
/// a host would — MemoryServiceBuilder over the production JsonlMemoryStore (temp dir),
/// the deterministic ClusterHashEmbedder, real LexicalIndex + the real engine backends —
/// and exercised only through the public IMemoryService surface. Pure library scope:
/// no host product of any kind participates — storage is a temp dir, embedders are
/// deterministic fakes, and nothing beyond the packages under test is referenced.
/// </summary>
public static class SystemRig
{
    public const int Dim = 128;

    /// <summary>Creates a fresh unique temp root directory ("temp-dir storage").</summary>
    public static string FreshRoot(string tag)
    {
        var path = Path.Combine(Path.GetTempPath(), "pvmx-systemtests", $"{tag}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>The ClusterHashEmbedder: fake, deterministic, cluster-preserving (no real model).</summary>
    public static ClusterHashEmbedder Embedder(int dim = Dim) => new(dim);

    /// <summary>
    /// Builds a MemoryService the production way over the JSONL store. All parameters
    /// optional so tests can specialize one aspect at a time.
    /// </summary>
    public static MemoryService BuildService(
        string root,
        IVectorIndexFactory? indexFactory = null,
        IndexKind indexKind = IndexKind.Flat,
        FusionOptions? fusion = null,
        ConsolidationOptions? consolidation = null,
        bool graph = false,
        ClusterHashEmbedder? embedder = null)
    {
        var embedderOwned = embedder ?? Embedder();
        var layout = new MemoryRootLayout(root, embedderOwned.ModelHash);
        var builder = new MemoryServiceBuilder()
            .WithOptions(new MemoryServiceOptions(
                Storage: new StorageOptions(root),
                Vector: new VectorOptions(embedderOwned.ModelHash, embedderOwned.Dim, indexKind),
                Graph: GraphOptions.Default,
                Fusion: fusion ?? new FusionOptions(),
                Consolidation: consolidation ?? new ConsolidationOptions()))
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

        return builder.Build();
    }

    /// <summary>
    /// Builds a fresh service over an EXISTING root (restart simulation): identical
    /// wiring, new instances everywhere.
    /// </summary>
    public static MemoryService AttachService(string root, bool graph = false) =>
        BuildService(root, graph: graph);

    /// <summary>IVF factory with compact deterministic settings for small corpora.</summary>
    public static EngineVectorIndexFactory IvfFactory() =>
        new(new EngineVectorIndexFactory.Options(
            new FlatExactIndex.Options(),
            new IvfFlatIndex.Options(nlist: 4, nprobe: 4, kmeansIterations: 12, seed: 2026),
            HnswOptions.Default));

    /// <summary>HNSW factory with compact deterministic settings.</summary>
    public static EngineVectorIndexFactory HnswFactory() =>
        new(new EngineVectorIndexFactory.Options(
            new FlatExactIndex.Options(),
            new IvfFlatIndex.Options(),
            new HnswOptions(M: 8, MaxLevel0Connections: 16, LevelFactor: 0.65, EfConstruction: 120, EfSearch: 96, Seed: 1729)));

    /// <summary>GPU factory (transparent CPU fallback keeps semantics identical).</summary>
    public static GpuVectorIndexFactory GpuFactory() => new(new GpuAccelerator(), new EngineVectorIndexFactory());

    /// <summary>
    /// FlatJson factory: one VectorNamespaceFile per scope-index the service creates
    /// (lazy per-scope), rooted under the store's root for realistic restart behavior.
    /// </summary>
    public sealed class FlatJsonFactory : IVectorIndexFactory
    {
        private readonly string _dir;
        private readonly string _modelHash;
        private int _created;

        public FlatJsonFactory(string root, string modelHash)
        {
            _dir = Path.Combine(root, "systemtests-vector-index");
            Directory.CreateDirectory(_dir);
            _modelHash = modelHash;
        }

        public IVectorIndex Create(IndexKind kind, int dim)
        {
            _created++;
            var file = new VectorNamespaceFile(Path.Combine(_dir, $"idx-{_created}.json"), _modelHash, dim);
            return new FlatJsonVectorIndex(file, new VectorIndexGuard(_modelHash, dim));
        }
    }

    /// <summary>
    /// Factory that counts every Create(kind) request — used to prove each configured
    /// IndexKind is actually routed to and instantiated (backend routing per options).
    /// </summary>
    public sealed class CaptureFactory : IVectorIndexFactory
    {
        private readonly IVectorIndexFactory _inner;

        public CaptureFactory(IVectorIndexFactory inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public List<IndexKind> Requests { get; } = new();

        public IVectorIndex Create(IndexKind kind, int dim)
        {
            Requests.Add(kind);
            return _inner.Create(kind, dim);
        }

        public int CountOf(IndexKind kind) => Requests.Count(kindRequest => kindRequest == kind);
    }
}

/// <summary>Small deterministic four-topic document set used by integration backends tests.</summary>
public static class TopicCorpus
{
    public static readonly (string Id, string Content)[] Documents =
    {
        ("kube-1", "kubernetes helm rollout notes summary"),
        ("kube-2", "kubernetes scale rollout context"),
        ("kube-3", "kubernetes helm chart background"),
        ("pg-1", "postgres vacuum index notes summary"),
        ("pg-2", "postgres migration lock context"),
        ("pg-3", "postgres tablespace vacuum background"),
        ("bill-1", "invoice stripe refund notes summary"),
        ("bill-2", "invoice billing chargeback context"),
        ("bill-3", "stripe receipt refund background"),
        ("bake-1", "sourdough starter ferment notes summary"),
        ("bake-2", "sourdough banneton crumb context"),
        ("bake-3", "sourdough ferment oven background"),
    };

    public static readonly string[] Queries =
    {
        "kubernetes helm rollout",
        "postgres vacuum migration",
        "invoice stripe refund",
        "sourdough starter ferment",
    };

    /// <summary>Records built over the given scope (fresh instances each call).</summary>
    public static IReadOnlyList<MemoryRecord> Records(MemoryScope scope) =>
        Documents
            .Select(document => new MemoryRecord(document.Id, scope, document.Id, document.Content))
            .ToList();

    /// <summary>Expected topic cluster per query in the service's deterministic RRF order
    /// (probe-verified against the real composition; ties break by id, but within-cluster
    /// lexical evidence can outrank id order — captured exactly here).</summary>
    public static string[][] ExpectedClusters { get; } =
    {
        new[] { "kube-1", "kube-2", "kube-3" },
        new[] { "pg-2", "pg-1", "pg-3" },
        new[] { "bill-1", "bill-3", "bill-2" },
        new[] { "bake-1", "bake-3", "bake-2" },
    };
}

/// <summary>Temp-root disposal helper shared by fixtures.</summary>
public sealed class TempRoot : IDisposable
{
    public TempRoot(string tag)
    {
        Path_ = SystemRig.FreshRoot(tag);
    }

    public string Path_ { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path_, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}