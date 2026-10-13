using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;
using ProvostXMemory.VectorIndex.Gpu;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

using Xunit;

namespace ProvostXMemory.GpuVectorIndexTests;

/// <summary>
/// Package-level safety tests: no-GPU hand-shake behavior, lazy kernel loading, and
/// determinism (CPU result == GPU-package result exactly, on any backend mix).
/// </summary>
public sealed class GpuAvailabilityAndDeterminismTests
{
    private const int Dim = 8;
    private const string ModelHash = "gpu-det-hash-v1";

    private static VectorIndexGuard Guard() => new(ModelHash, Dim);

    private static List<VectorItem> Corpuse(int count = 40)
    {
        var items = new List<VectorItem>(count);
        for (var i = 0; i < count; i++)
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

    // ---------- availability gate: CPU machines must never see exceptions ----------

    [Fact]
    public void GpuAccelerator_handshake_is_safe_and_lazy_on_cpu_machine()
    {
        using var gpu = new GpuAccelerator();
        // Lazy: construction alone must not resolve; touching the flag resolves safely.
        var isAvailable = gpu.IsAvailable;
        var name = gpu.Name;
        var description = gpu.DeviceDescription;

        // On any machine the values are consistent; without CUDA/OpenCL: false/empty.
        if (!isAvailable)
        {
            Assert.Equal(string.Empty, name);
            Assert.Equal(string.Empty, description);
            Assert.Throws<InvalidOperationException>(() => gpu.RequireAccelerator());
        }
        else
        {
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.False(string.IsNullOrWhiteSpace(description));
        }
    }

    [Fact]
    public void GpuAccelerator_dispose_is_idempotent_and_flags_unavailable()
    {
        var gpu = new GpuAccelerator();
        _ = gpu.IsAvailable; // force resolution (or safe failure)
        gpu.Dispose();
        gpu.Dispose(); // idempotent
        Assert.False(gpu.IsAvailable);
    }

    [Fact]
    public void GpuAccelerator_cuda_opencl_only_context_on_cpu_box_is_unavailable()
    {
        var policy = new DefaultGpuContextPolicy(allowCpuFallback: false);
        using var gpu = new GpuAccelerator(
            policy,
            new GpuDiscoveryOptions(tryCuda: true, tryOpenCl: true),
            new GpuLaunchOptions());
        if (!HasAnyGpu())
        {
            Assert.False(gpu.IsAvailable);
            Assert.Equal(string.Empty, gpu.Name);
        }
    }

    private static bool HasAnyGpu()
    {
        try
        {
            using var context = ILGPU.Context.Create(builder =>
            {
                builder.Cuda();
                builder.OpenCL();
            });
            return context.GetCudaDevices().Count > 0 || context.GetCLDevices().Count > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ---------- fallback is wired and IDENTICAL when no GPU is present ----------

    [Fact]
    public async Task GpuFlatIndex_without_gpu_behaves_exactly_like_fallback()
    {
        var guard = Guard();
        using var gpu = new GpuAccelerator();
        var flat = new FlatExactIndex(guard);
        var gpuFlat = new GpuFlatIndex(gpu, guard, flat);

        var corpus = Corpuse();
        await gpuFlat.BuildAsync(corpus);

        if (!gpu.IsAvailable)
        {
            // The package must have mirrored everything into the CPU fallback.
            Assert.Equal(flat.Count, gpuFlat.Count);
            foreach (var item in corpus.Take(5))
            {
                var fromCpu = await flat.SearchAsync(item.Vector, 3);
                var fromGpu = await gpuFlat.SearchAsync(item.Vector, 3);
                Assert.Equal(fromCpu.Select(static h => h.Id), fromGpu.Select(static h => h.Id));
                Assert.Equal(fromCpu.Select(static h => h.Score), fromGpu.Select(static h => h.Score));
            }
        }
    }

    // ---------- determinism: GPU-package result == CPU result EXACTLY ----------

    [Fact]
    public async Task GpuFlat_matches_cpu_exactly_rank_and_scores()
    {
        var guard = Guard();
        var flat = new FlatExactIndex(guard);
        var gpuFlat = new GpuFlatIndex(new GpuAccelerator(), guard, flat);

        var corpus = Corpuse();
        await flat.BuildAsync(corpus);
        await gpuFlat.BuildAsync(corpus);

        for (var q = 0; q < corpus.Count; q += 3)
        {
            var query = corpus[q].Vector;
            var cpuHits = await flat.SearchAsync(query, 10);
            var gpuHits = await gpuFlat.SearchAsync(query, 10);

            Assert.Equal(cpuHits.Count, gpuHits.Count);
            for (var h = 0; h < cpuHits.Count; h++)
            {
                Assert.Equal(cpuHits[h].Id, gpuHits[h].Id);
                Assert.Equal(cpuHits[h].Score, gpuHits[h].Score); // bit-exact
            }
        }
    }

    [Fact]
    public async Task GpuFlat_matches_cpu_on_perturbed_queries_and_all_metrics()
    {
        foreach (var metric in new[] { VectorDistanceMetric.Cosine, VectorDistanceMetric.NegativeSquaredEuclidean })
        {
            var guard = Guard();
            var flat = new FlatExactIndex(guard, new FlatExactIndex.Options(metric));
            var gpuFlat = new GpuFlatIndex(
                new GpuAccelerator(),
                guard,
                flat,
                new GpuFlatIndex.Options(metric, new GpuLaunchOptions()));

            var corpus = Corpuse();
            await flat.BuildAsync(corpus);
            await gpuFlat.BuildAsync(corpus);

            for (var q = 0; q < corpus.Count; q += 4)
            {
                var query = (float[])corpus[q].Vector.ToArray();
                for (var d = 0; d < Dim; d++)
                {
                    query[d] += 0.013f * (d + 1);
                }

                var cpuHits = await flat.SearchAsync(query, 5);
                var gpuHits = await gpuFlat.SearchAsync(query, 5);

                Assert.Equal(cpuHits.Select(static h => h.Id), gpuHits.Select(static h => h.Id));
                Assert.Equal(cpuHits.Select(static h => h.Score), gpuHits.Select(static h => h.Score));
            }
        }
    }

    [Fact]
    public async Task GpuIvf_matches_cpu_exactly_rank_and_scores()
    {
        var guard = Guard();
        var options = new GpuIvfIndex.Options(nlist: 4, nprobe: 4, kmeansIterations: 30, seed: 1729);
        var cpuOptions = new IvfFlatIndex.Options(nlist: 4, nprobe: 4, kmeansIterations: 30, seed: 1729);

        var cpuIndex = new IvfFlatIndex(guard, cpuOptions);
        var gpuIndex = new GpuIvfIndex(new GpuAccelerator(), guard, cpuIndex, options);

        // Well-separated clusters: IVF probing is complete, so results must match CPU exactly.
        var corpus = ClusteredCorpus(perCluster: 10, clusters: 4);
        await cpuIndex.BuildAsync(corpus);
        await gpuIndex.BuildAsync(corpus);

        for (var c = 0; c < 4; c++)
        {
            for (var variant = 0; variant < 2; variant++)
            {
                var query = ClusterPoint(c, variant, jitter: 0.02f);
                var cpuHits = await cpuIndex.SearchAsync(query, 10);
                var gpuHits = await gpuIndex.SearchAsync(query, 10);

                Assert.Equal(cpuHits.Select(static h => h.Id), gpuHits.Select(static h => h.Id));
                Assert.Equal(cpuHits.Select(static h => h.Score), gpuHits.Select(static h => h.Score));
            }
        }
    }

    [Fact]
    public async Task GpuIvf_partition_matches_engine_partition_on_same_data()
    {
        var guard = Guard();
        var cpuIndex = new IvfFlatIndex(
            guard,
            new IvfFlatIndex.Options(nlist: 4, nprobe: 2, kmeansIterations: 25, seed: 1729));
        var gpuIndex = new GpuIvfIndex(
            new GpuAccelerator(),
            guard,
            cpuIndex,
            new GpuIvfIndex.Options(nlist: 4, nprobe: 2, kmeansIterations: 25, seed: 1729));

        var corpus = Corpuse(30);
        await cpuIndex.BuildAsync(corpus);
        await gpuIndex.BuildAsync(corpus);

        var cpuIvf = Assert.IsType<IvfFlatIndex>(cpuIndex);
        Assert.Equal(cpuIvf.ClusterCount, gpuIndex.ClusterCount);
    }

    // ---------- fixture helpers (clustered corpus for IVF) ----------

    private static List<VectorItem> ClusteredCorpus(int perCluster, int clusters)
    {
        var items = new List<VectorItem>(perCluster * clusters);
        for (var c = 0; c < clusters; c++)
        {
            for (var v = 0; v < perCluster; v++)
            {
                items.Add(new VectorItem($"c{c}-v{v:D2}", ClusterPoint(c, v, jitter: 0.01f)));
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