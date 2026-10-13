using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;
using ProvostXMemory.VectorIndex.Gpu;
using ProvostXMemory.VectorIndexTests;

using Xunit;

namespace ProvostXMemory.VectorIndex.GpuTests;

/// <summary>
/// Conformance fixture for the ILGPU GPU backend. On this machine no discrete GPU is
/// present (ILGPU CPUAccelerator only), so the conformance suite exercises the
/// transparent CPU fallback path; on GPU-equipped machines it exercises the kernel path.
/// GpuFlatIndex is an exact backend: default ExactRanking=true applies.
/// </summary>
public sealed class GpuFlatIndexConformanceTests : VectorIndexConformanceTests
{
    /// <summary>Dim used by the conformance fixtures (kept in sync with the base class).</summary>
    public const int TestDim = 8;

    public const string TestModelHash = "test-model-hash-v1";

    protected override IVectorIndex CreateIndex()
    {
        return new GpuFlatIndex(
            new VectorIndexGuard(TestModelHash, TestDim),
            new FlatExactIndex(new VectorIndexGuard(TestModelHash, TestDim)));
    }
}