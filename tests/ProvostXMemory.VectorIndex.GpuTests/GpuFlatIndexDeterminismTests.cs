using System.Collections.Generic;

using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;
using ProvostXMemory.VectorIndex.Gpu;
using ProvostXMemory.VectorIndexTests;

using Xunit;

namespace ProvostXMemory.VectorIndex.GpuTests;

/// <summary>
/// Determinism contract: whatever path GpuFlatIndex takes (ILGPU kernel here — this
/// machine still exposes a CPUAccelerator — or pure CPU fallback on init failure), the
/// ranking must equal the FlatExactIndex baseline exactly: same ids, same scores,
/// same order.
/// </summary>
public sealed class GpuFlatIndexDeterminismTests
{
    private const int Dim = 8;
    private const string ModelHash = "test-model-hash-v1";

    private static List<VectorItem> Corpus()
    {
        var items = new List<VectorItem>();
        for (var i = 0; i < 40; i++)
        {
            var vector = new float[Dim];
            for (var d = 0; d < Dim; d++)
            {
                vector[d] = MathF.Sin(((i + 1) * (d + 3)) * 0.37f) * (1f + (i % 5));
            }

            items.Add(new VectorItem($"id-{i:D2}", vector));
        }

        return items;
    }

    private static float[] Query(int i)
    {
        var query = new float[Dim];
        for (var d = 0; d < Dim; d++)
        {
            query[d] = MathF.Sin(((i + 1) * (d + 3)) * 0.37f) * (1f + (i % 5));
        }

        for (var d = 0; d < Dim; d++)
        {
            query[d] += 0.01f * d;
        }

        return query;
    }

    [Fact]
    public async System.Threading.Tasks.Task Gpu_results_match_FlatExactIndex_exactly()
    {
        var corpus = Corpus();

        var baseline = new FlatExactIndex(new VectorIndexGuard(ModelHash, Dim));
        await baseline.BuildAsync(corpus);

        var gpu = new GpuFlatIndex(
            new VectorIndexGuard(ModelHash, Dim),
            new FlatExactIndex(new VectorIndexGuard(ModelHash, Dim)));
        await gpu.BuildAsync(corpus);

        for (var i = 0; i < corpus.Count; i += 3)
        {
            var query = Query(i);

            var expected = await baseline.SearchAsync(query, 10);
            var actual = await gpu.SearchAsync(query, 10);

            Assert.Equal(expected.Count, actual.Count);
            for (var hit = 0; hit < expected.Count; hit++)
            {
                Assert.Equal(expected[hit].Id, actual[hit].Id);
                Assert.Equal(expected[hit].Score, actual[hit].Score);
            }
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task Gpu_results_survive_mutation_sequence_exactly()
    {
        var corpus = Corpus();

        var baseline = new FlatExactIndex(new VectorIndexGuard(ModelHash, Dim));
        var gpu = new GpuFlatIndex(
            new VectorIndexGuard(ModelHash, Dim),
            new FlatExactIndex(new VectorIndexGuard(ModelHash, Dim)));

        await baseline.BuildAsync(corpus);
        await gpu.BuildAsync(corpus);

        var extra = new float[Dim];
        for (var d = 0; d < Dim; d++)
        {
            extra[d] = 0.5f + (0.125f * d);
        }

        await baseline.AddAsync(new VectorItem("extra-1", extra));
        await gpu.AddAsync(new VectorItem("extra-1", extra));

        await baseline.RemoveAsync("id-07");
        await gpu.RemoveAsync("id-07");

        Assert.Equal(baseline.Count, gpu.Count);

        for (var i = 0; i < corpus.Count; i += 7)
        {
            var query = Query(i);
            var expected = await baseline.SearchAsync(query, 5);
            var actual = await gpu.SearchAsync(query, 5);

            Assert.Equal(expected.Count, actual.Count);
            for (var hit = 0; hit < expected.Count; hit++)
            {
                Assert.Equal(expected[hit].Id, actual[hit].Id);
                Assert.Equal(expected[hit].Score, actual[hit].Score);
            }
        }
    }
}