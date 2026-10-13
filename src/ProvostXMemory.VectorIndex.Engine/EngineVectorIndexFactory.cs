using System;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.VectorIndex.Engine;

/// <summary>Own-engine factory: Flat → FlatExactIndex, Ivf → IvfFlatIndex, Hnsw → HnswIndex.</summary>
public sealed class EngineVectorIndexFactory : IVectorIndexFactory
{
    /// <summary>Backend options for every created index.</summary>
    public sealed class Options
    {
        public Options()
            : this(new FlatExactIndex.Options(), new IvfFlatIndex.Options(), new HnswOptions())
        {
        }

        public Options(FlatExactIndex.Options flatOptions, IvfFlatIndex.Options ivfOptions, HnswOptions hnswOptions)
        {
            ArgumentNullException.ThrowIfNull(flatOptions);
            ArgumentNullException.ThrowIfNull(ivfOptions);
            ArgumentNullException.ThrowIfNull(hnswOptions);
            Flat = flatOptions;
            Ivf = ivfOptions;
            Hnsw = hnswOptions;
        }

        public FlatExactIndex.Options Flat { get; }

        public IvfFlatIndex.Options Ivf { get; }

        public HnswOptions Hnsw { get; }
    }

    private readonly Options _options;

    public EngineVectorIndexFactory()
        : this(new Options())
    {
    }

    public EngineVectorIndexFactory(Options options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public IVectorIndex Create(IndexKind kind, int dim)
    {
        if (dim <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dim));
        }

        var guard = new VectorIndexGuard("engine-factory", dim);
        return kind switch
        {
            IndexKind.Flat => new FlatExactIndex(guard, _options.Flat),
            IndexKind.Ivf => new IvfFlatIndex(guard, _options.Ivf),
            IndexKind.Hnsw => new HnswIndex(guard, _options.Hnsw),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported index kind."),
        };
    }
}