using System;
using System.IO;

using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;
using ProvostXMemory.VectorIndex.Gpu;

using Xunit;

namespace ProvostXMemory.GpuVectorIndexTests;

/// <summary>
/// Conformance fixture for GpuIvfIndex. Exact-ranking tests are replaced by the
/// documented recall@10 ≥ 0.95 tolerance (same gate as the engine IvfFlatIndex).
/// </summary>
public sealed class GpuIvfIndexConformanceTests : GpuVectorIndexConformanceTests
{
    public const int TestDim = 8;

    public const string TestModelHash = "test-model-hash-v1";

    /// <summary>Documented recall threshold (mirrors the engine IVF gate).</summary>
    public const double MinRecallAt10 = 0.95;

    protected override IVectorIndex CreateIndex()
    {
        var guard = new VectorIndexGuard(TestModelHash, TestDim);
        return new GpuIvfIndex(
            new GpuAccelerator(),
            guard,
            new IvfFlatIndex(
                guard,
                new IvfFlatIndex.Options(nlist: 4, nprobe: 4, kmeansIterations: 30, seed: 1729)),
            new GpuIvfIndex.Options(nlist: 4, nprobe: 4, kmeansIterations: 30, seed: 1729));
    }

    protected override bool ExactRanking => false;
}