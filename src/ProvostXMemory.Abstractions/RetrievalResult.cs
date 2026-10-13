using System.Collections.Generic;

namespace ProvostXMemory.Abstractions;

/// <summary>
/// <para>The outcome of <c>IMemoryService.RecallAsync</c>: the scored records,
/// best first, plus per-stage diagnostics.</para>
/// <para><see cref="Records"/> is ordered by <see cref="ScoredMemoryRecord.Score"/>
/// descending with deterministic tie-breaks by record id. <see cref="Diagnostics"/> carries
/// one entry per pipeline stage (dense, lexical, fusion, rerank, corrective retries) with
/// durations and stage-specific metrics — feed it to the eval harness for Recall@k/MRR/NDCG.</para>
/// </summary>
public sealed record RetrievalResult(
    IReadOnlyList<ScoredMemoryRecord> Records,
    IReadOnlyList<RetrievalStageDiagnostics>? Diagnostics = null)
{
    private static readonly IReadOnlyList<RetrievalStageDiagnostics> EmptyDiagnostics =
        Array.Empty<RetrievalStageDiagnostics>();

    public IReadOnlyList<RetrievalStageDiagnostics> Diagnostics { get; init; } = Diagnostics ?? EmptyDiagnostics;
}