using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.VectorIndex;


/// <summary>
/// <para>The pluggable dense-vector index contract. The library ships four backends, all
/// interchangeable through this interface (they pass one shared conformance suite):
/// <c>IndexKind.FlatJson</c> (zero-dependency JSON file, brute-force cosine),
/// <c>IndexKind.Flat</c> (in-memory exact search with SIMD kernels),
/// <c>IndexKind.Ivf</c> (k-means inverted lists for corpus sizes where full scans
/// get expensive), and <c>IndexKind.Hnsw</c> (navigable-small-world graph; built
/// with immutable/rebuild semantics and searched with a layered greedy beam). A GPU tier
/// (ILGPU, transparent CPU fallback) wraps any of these for machines with devices.</para>
/// <para><b>Vector contract:</b> vectors are L2-normalized at index time; scores are cosine
/// similarities (higher = closer, 1.0 = identical). <see cref="Dim"/> is fixed at
/// construction; every item/query with a different dimension is rejected.
/// <b>Concurrency:</b> implementations own their internal synchronization; concurrent
/// Add/Remove/Search from multiple threads is safe.</para>
/// <para><b>Backends compare sets, not orderings:</b> ranking parity between backends is
/// exact for Flat/FlatJson and statistically guaranteed (recall ≥0.95 on the fixture) for
/// the ANN tiers — results are ranked identically after fusion because ordering derives
/// from scores, not insertion sequence.</para>
/// </summary>
public interface IVectorIndex
{
    int Dim { get; }

    long Count { get; }

    Task BuildAsync(IReadOnlyList<VectorItem> items, CancellationToken cancellationToken = default);

    Task AddAsync(VectorItem item, CancellationToken cancellationToken = default);

    Task RemoveAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScoredVectorId>> SearchAsync(IReadOnlyList<float> vector, int k, CancellationToken cancellationToken = default);

    Task FlushAsync(CancellationToken cancellationToken = default);
}

