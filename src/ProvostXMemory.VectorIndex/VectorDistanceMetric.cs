using System;
using System.Collections.Generic;

namespace ProvostXMemory.VectorIndex;

/// <summary>Distance metric for vector-to-vector comparisons inside the engine.</summary>
public enum VectorDistanceMetric
{
    /// <summary>Cosine similarity in [-1, 1]; higher = more similar (exact ranking metric).</summary>
    Cosine,

    /// <summary>Squared L2 distance; reported negated so that higher score = closer.</summary>
    NegativeSquaredEuclidean,
}