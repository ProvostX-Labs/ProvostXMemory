namespace ProvostXMemory.Abstractions;

/// <summary>
/// The vector index backend family. <see cref="Flat"/> — exact SIMD scan, the correctness
/// baseline and the best choice up to tens of thousands of vectors. <see cref="Ivf"/> —
/// deterministic k-means inverted lists; probes only <c>nprobe</c> clusters per query,
/// the sweet spot for large corpora. <see cref="Hnsw"/> — navigable-small-world graph,
/// highest recall per query latency; built with immutable-index/rebuild semantics and
/// searched with a layered greedy beam (see the architecture patent addendum). <see cref="Gpu"/>
/// — ILGPU-kernel decoration over a CPU backend with bit-exact fallback when no device exists.
/// All kinds pass one shared conformance suite; results are interchangeable between kinds
/// (FlatJson included, see <c>IVectorIndex</c>).
/// </summary>
public enum IndexKind
{
    Flat,
    Ivf,
    Hnsw,
    Gpu,
}