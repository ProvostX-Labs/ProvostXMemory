using System;
using System.IO;

using ProvostXMemory.VectorIndex;

using Xunit;

namespace ProvostXMemory.VectorIndexTests;

/// <summary>Conformance fixture for the FlatJson backend (JSON namespace file + cosine).</summary>
public sealed class FlatJsonVectorIndexConformanceTests : VectorIndexConformanceTests
{
    /// <summary>Dim used by the conformance fixtures (kept in sync with the base class).</summary>
    public const int TestDim = 8;

    public const string TestModelHash = "test-model-hash-v1";

    private readonly string _tempDir;

    public FlatJsonVectorIndexConformanceTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("pvx-vecidx-flatjson-").FullName;
    }

    protected override IVectorIndex CreateIndex()
    {
        var path = Path.Combine(_tempDir, $"idx-{Guid.NewGuid():N}.json");
        return new FlatJsonVectorIndex(
            new VectorNamespaceFile(path, TestModelHash, TestDim),
            new VectorIndexGuard(TestModelHash, TestDim));
    }
}