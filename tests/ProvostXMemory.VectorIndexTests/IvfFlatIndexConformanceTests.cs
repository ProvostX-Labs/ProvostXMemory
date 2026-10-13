using System;
using System.IO;

using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;

using Xunit;

namespace ProvostXMemory.VectorIndexTests;

/// <summary>
/// Conformance fixture for the own-engine IVF-Flat backend. Exact-ranking search tests are
/// replaced by recall@10 ≥ 0.95 on identical fixtures (documented tolerance).
/// </summary>
public sealed class IvfFlatIndexConformanceTests : VectorIndexConformanceTests
{
    /// <summary>Dim used by the conformance fixtures (kept in sync with the base class).</summary>
    public const int TestDim = 8;

    public const string TestModelHash = "test-model-hash-v1";

    /// <summary>Documented recall threshold for IVF on the conformance fixtures.</summary>
    public const double MinRecallAt10 = 0.95;

    protected override IVectorIndex CreateIndex()
    {
        return new IvfFlatIndex(
            new VectorIndexGuard(TestModelHash, TestDim),
            new IvfFlatIndex.Options(nlist: 4, nprobe: 4, kmeansIterations: 30, seed: 1729));
    }

    protected override bool ExactRanking => false;
}