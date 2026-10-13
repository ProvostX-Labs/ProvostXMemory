using System;

namespace ProvostXMemory.VectorIndex;

/// <summary>Scans rows with the metric's SIMD kernel; shared scoring core for flat/IVF search.</summary>
public sealed class VectorScanner
{
    private readonly VectorDistanceMetric _metric;

    public VectorScanner(VectorDistanceMetric metric)
    {
        _metric = metric;
    }

    public VectorDistanceMetric Metric => _metric;

    /// <summary>Exact score of one candidate row under the metric (higher = closer).</summary>
    public float Score(ReadOnlySpan<float> query, ReadOnlySpan<float> candidate)
    {
        return _metric switch
        {
            VectorDistanceMetric.Cosine => VectorMath.Cosine(query, candidate),
            VectorDistanceMetric.NegativeSquaredEuclidean => -SquaredDistance(query, candidate),
            _ => throw new InvalidOperationException($"Unknown metric {_metric}."),
        };
    }

    /// <summary>Squared L2 distance via SIMD dot; scalar-tail handled by VectorMath.Dot.</summary>
    public float SquaredDistance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var dot = VectorMath.Dot(a, b);
        var normSum = VectorMath.Dot(a, a) + VectorMath.Dot(b, b);
        return normSum - (2f * dot);
    }
}