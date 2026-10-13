namespace ProvostXMemory.VectorIndex.Gpu;

/// <summary>Backend metric shared with the engine kernels (mirrors VectorDistanceMetric).</summary>
public enum GpuDistanceMetric
{
    /// <summary>Cosine similarity; higher = closer.</summary>
    Cosine = 0,

    /// <summary>Squared L2 distance reported negated; higher = closer.</summary>
    NegativeSquaredEuclidean = 1,
}