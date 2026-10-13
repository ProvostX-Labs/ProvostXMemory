using System;


namespace ProvostXMemory.VectorIndex.Gpu;

/// <summary>Mapping from the engine metric enum onto the GPU kernel's int metric code.</summary>
public static class GpuMetricCodec
{
    /// <summary>Encodes the engine metric for GPU kernel consumption.</summary>
    public static int Encode(VectorDistanceMetric metric)
    {
        return metric switch
        {
            VectorDistanceMetric.Cosine => (int)GpuDistanceMetric.Cosine,
            VectorDistanceMetric.NegativeSquaredEuclidean => (int)GpuDistanceMetric.NegativeSquaredEuclidean,
            _ => throw new ArgumentOutOfRangeException(nameof(metric)),
        };
    }
}