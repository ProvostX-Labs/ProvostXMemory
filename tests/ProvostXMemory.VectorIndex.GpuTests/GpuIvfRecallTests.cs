using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;
using ProvostXMemory.VectorIndex.Gpu;

using Xunit;

namespace ProvostXMemory.GpuVectorIndexTests;

/// <summary>
/// Recall gate for GpuIvfIndex on clustered fixtures — identical methodology to the
/// engine IvfFlatIndex recall test (recall@10 ≥ 0.95 vs FlatExactIndex truth).
/// </summary>
public sealed class GpuIvfRecallTests
{
    private const int Dim = 8;
    private const string ModelHash = "gpu-recall-hash-v1";
    private const double MinRecallAt10 = 0.95;

    [Fact]
    public async Task RecallAt10_meets_documented_threshold_vs_flat()
    {
        var guard = new VectorIndexGuard(ModelHash, Dim);
        var corpus = ClusteredCorpus(perCluster: 15, clusters: 6);

        var flat = new FlatExactIndex(guard);
        await flat.BuildAsync(corpus);

        var ivf = new GpuIvfIndex(
            new GpuAccelerator(),
            guard,
            new IvfFlatIndex(
                guard,
                new IvfFlatIndex.Options(nlist: 6, nprobe: 2, kmeansIterations: 25, seed: 1729)),
            new GpuIvfIndex.Options(nlist: 6, nprobe: 2, kmeansIterations: 25, seed: 1729));
        await ivf.BuildAsync(corpus);

        double totalRecall = 0;
        var queryCount = 0;
        for (var c = 0; c < 6; c++)
        {
            for (var variant = 0; variant < 3; variant++)
            {
                var query = ClusterPoint(c, variant, jitter: 0.03f);
                var truthIds = (await flat.SearchAsync(query, 10)).Select(static hit => hit.Id).ToHashSet();
                var ivfIds = (await ivf.SearchAsync(query, 10)).Select(static hit => hit.Id).ToHashSet();
                var overlap = truthIds.Count(ivfIds.Contains);
                totalRecall += (double)overlap / truthIds.Count;
                queryCount++;
            }
        }

        var meanRecall = totalRecall / queryCount;
        Assert.True(
            meanRecall >= MinRecallAt10,
            $"recall@10 {meanRecall:F3} below documented threshold {MinRecallAt10}");
    }

    private static List<VectorItem> ClusteredCorpus(int perCluster, int clusters)
    {
        var items = new List<VectorItem>(perCluster * clusters);
        for (var c = 0; c < clusters; c++)
        {
            for (var v = 0; v < perCluster; v++)
            {
                items.Add(new VectorItem($"c{c}-v{v:D2}", ClusterPoint(c, v, jitter: 0.01f * v)));
            }
        }

        return items;
    }

    private static float[] ClusterPoint(int cluster, int variant, float jitter)
    {
        var vector = new float[Dim];
        for (var d = 0; d < Dim; d++)
        {
            var angle = (cluster * 2.1f) + (d * 0.7f) + (variant * jitter * (d + 1));
            vector[d] = MathF.Cos(angle) + (cluster * 0.35f);
        }

        return vector;
    }
}