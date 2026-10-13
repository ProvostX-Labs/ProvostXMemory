using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;

using Xunit;

namespace ProvostXMemory.VectorIndexTests;

/// <summary>
/// Conformance fixture for the own-engine HNSW backend (immutable-index + batch-rebuild
/// semantics). Approximate backend: exact-ranking search test replaced by recall@10 ≥ 0.95
/// on identical fixtures (documented tolerance), same as IVF.
/// </summary>
public sealed class HnswIndexConformanceTests : VectorIndexConformanceTests
{
    /// <summary>Dim used by the conformance fixtures (kept in sync with the base class).</summary>
    public const int TestDim = 8;

    public const string TestModelHash = "test-model-hash-v1";

    /// <summary>Documented recall threshold for HNSW on the conformance fixtures.</summary>
    public const double MinRecallAt10 = 0.95;

    protected override IVectorIndex CreateIndex()
    {
        return new HnswIndex(
            new VectorIndexGuard(TestModelHash, TestDim),
            new HnswOptions(M: 8, MaxLevel0Connections: 16, LevelFactor: 0.65, EfConstruction: 120, EfSearch: 96, Seed: 1729));
    }

    protected override bool ExactRanking => false;
}