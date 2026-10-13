using System;
using System.Collections.Generic;

namespace ProvostXMemory.VectorIndex;

/// <summary>
/// OWN deterministic k-means (Lloyd iterations, seeded) — the IVF coarse quantizer.
/// Deterministic given the same training rows: seeded Fisher-Yates seeding, squared-L2
/// assignment via the SIMD scanner, mean update, empty clusters reseated on the globally
/// farthest points (deterministic order). Internally uses squared L2 regardless of the
/// search metric — IVF coarse assignment is metric-agnostic partitioning.
/// </summary>
public sealed class KMeansQuantizer
{
    private readonly SeededRandom _random;
    private readonly VectorScanner _scanner;

    /// <summary>Seed fixed at construction: same seed + data → same centroids.</summary>
    public KMeansQuantizer(int seed)
    {
        Seed = seed;
        _random = new SeededRandom(seed);
        _scanner = new VectorScanner(VectorDistanceMetric.NegativeSquaredEuclidean);
    }

    public int Seed { get; }

    /// <summary>Runs k-means over the training rows; labels align with the row order.</summary>
    public KMeansResult Cluster(float[][] rows, int k, int iterations)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Length == 0)
        {
            throw new ArgumentException("K-means requires at least one training row.", nameof(rows));
        }

        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k));
        }

        if (iterations < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations));
        }

        var clusterCount = Math.Min(k, rows.Length);
        var dim = rows[0].Length;
        foreach (var row in rows)
        {
            if (row.Length != dim)
            {
                throw new ArgumentException("All training rows must share one dimension.");
            }
        }

        var centroids = SeedCentroids(rows, clusterCount);
        var labels = new int[rows.Length];

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var moved = Assign(rows, centroids, labels);
            Update(rows, labels, centroids);
            if (moved == 0)
            {
                break;
            }
        }

        return new KMeansResult(centroids, labels);
    }

    private float[][] SeedCentroids(float[][] rows, int k)
    {
        // Deterministic seeded Fisher-Yates over row indices; first k shuffled rows are seeds.
        var indices = new int[rows.Length];
        for (var i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        var take = Math.Min(k, indices.Length);
        for (var i = 0; i < take; i++)
        {
            var j = _random.Next(i, indices.Length);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        var centroids = new float[k][];
        for (var c = 0; c < k; c++)
        {
            var source = c < take ? indices[c] : indices[^1];
            centroids[c] = (float[])rows[source].Clone();
        }

        return centroids;
    }

    private int Assign(float[][] rows, float[][] centroids, int[] labels)
    {
        var moved = 0;
        for (var r = 0; r < rows.Length; r++)
        {
            var best = -1;
            var bestScore = float.NegativeInfinity;
            for (var c = 0; c < centroids.Length; c++)
            {
                var score = _scanner.Score(rows[r], centroids[c]); // negative squared L2
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }

            if (labels[r] != best)
            {
                moved++;
            }

            labels[r] = best;
        }

        return moved;
    }

    private void Update(float[][] rows, int[] labels, float[][] centroids)
    {
        var dim = centroids[0].Length;
        var sums = new float[centroids.Length][];
        var counts = new int[centroids.Length];
        for (var c = 0; c < centroids.Length; c++)
        {
            sums[c] = new float[dim];
        }

        for (var r = 0; r < rows.Length; r++)
        {
            var sumsRow = sums[labels[r]];
            var row = rows[r];
            for (var d = 0; d < dim; d++)
            {
                sumsRow[d] += row[d];
            }

            counts[labels[r]]++;
        }

        // Empty clusters: reseat on the globally farthest rows (distance to own centroid),
        // deterministic by scanning rows in index order and never picking the same row twice.
        var reseated = new HashSet<int>();

        for (var c = 0; c < centroids.Length; c++)
        {
            if (counts[c] > 0)
            {
                for (var d = 0; d < dim; d++)
                {
                    centroids[c][d] = sums[c][d] / counts[c];
                }
            }
            else
            {
                var farthest = -1;
                var farthestDistance = float.NegativeInfinity;
                for (var r = 0; r < rows.Length; r++)
                {
                    if (reseated.Contains(r))
                    {
                        continue;
                    }

                    var d = -_scanner.Score(rows[r], centroids[labels[r]]);
                    if (d > farthestDistance)
                    {
                        farthestDistance = d;
                        farthest = r;
                    }
                }

                if (farthest >= 0)
                {
                    centroids[c] = (float[])rows[farthest].Clone();
                    reseated.Add(farthest);
                }
            }
        }
    }
}