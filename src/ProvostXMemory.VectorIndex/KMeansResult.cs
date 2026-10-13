using System;
using System.Collections.Generic;

namespace ProvostXMemory.VectorIndex;

/// <summary>Result of one k-means run: centroids plus label arrays over the training rows.</summary>
public sealed class KMeansResult
{
    public KMeansResult(float[][] centroids, int[] labels)
    {
        Centroids = centroids;
        Labels = labels;
    }

    /// <summary>Centroid row i ↔ cluster i.</summary>
    public float[][] Centroids { get; }

    /// <summary>Training row r → assigned centroid row (cluster id).</summary>
    public int[] Labels { get; }
}