namespace ProvostXMemory.VectorIndex.Engine;

/// <summary>
/// The published immutable snapshot of a built HNSW graph: row-order ids parallel to the
/// normalized vector rows and the graph topology. Swapped in atomically after each full
/// rebuild; never mutated afterwards (batch-rebuild semantics).
/// </summary>
public sealed record HnswPublishedGraph(string[] Ids, HnswGraph Graph, float[][] NormalizedRows);