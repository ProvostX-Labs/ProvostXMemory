namespace ProvostXMemory.Abstractions;

/// <summary>
/// A record matched by a retrieval, its relevance score, and where the score came from.
/// <see cref="Provenance"/> names the stage that produced the score and carries per-stage
/// subscores (e.g. the fused RRF score plus the rerank-adjusted score), so ranking changes
/// across pipeline stages remain explainable end to end.
/// </summary>
public sealed record ScoredMemoryRecord(
    MemoryRecord Record,
    double Score,
    RetrievalProvenance Provenance);