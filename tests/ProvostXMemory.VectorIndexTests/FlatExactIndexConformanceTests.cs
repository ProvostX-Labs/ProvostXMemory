using System;
using System.IO;

using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;

using Xunit;

namespace ProvostXMemory.VectorIndexTests;

/// <summary>Conformance fixture for the own-engine SIMD Flat exact backend.</summary>
public sealed class FlatExactIndexConformanceTests : VectorIndexConformanceTests
{
    /// <summary>Dim used by the conformance fixtures (kept in sync with the base class).</summary>
    public const int TestDim = 8;

    public const string TestModelHash = "test-model-hash-v1";

    protected override IVectorIndex CreateIndex()
    {
        return new FlatExactIndex(new VectorIndexGuard(TestModelHash, TestDim));
    }
}