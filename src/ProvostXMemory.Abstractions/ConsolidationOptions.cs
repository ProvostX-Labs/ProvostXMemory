namespace ProvostXMemory.Abstractions;

/// <summary>
/// <para>Tuning for one <c>IMemoryService.ConsolidateAsync</c> pass over a scope.</para>
/// <para><b><see cref="DedupSimilarityThreshold"/></b> — cosine above which two records count
/// as near-duplicates (lexical Jaccard fallback when embeddings degenerate). The OLDEST
/// record of each duplicate group is kept, tags are unioned into it, the rest are deleted.
/// <b><see cref="ExpireTtl"/></b> — when on (default), records whose <c>ExpiresAt</c> is past
/// are deleted first. <b><see cref="EmitClusterSummaries"/></b> — when on, the remaining
/// near-duplicate clusters (similarity ≥ the <c>ConsolidationTuning.ClusterJoinThreshold</c>)
/// emit one summary record each, graph-recorded when a graph extractor is wired.</para>
/// </summary>
public sealed record ConsolidationOptions(
    double DedupSimilarityThreshold = 0.92,
    bool ExpireTtl = true,
    bool EmitClusterSummaries = false);
