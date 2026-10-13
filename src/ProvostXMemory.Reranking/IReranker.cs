using System;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Reranking;

/// <summary>
/// <para>The post-fusion scoring stage: reranks fused candidates against the query.
/// The default <c>ScoreBoostReranker</c> adds deterministic boosts (tag/key/category term
/// matches) and penalties (extremely short or long content) without ever dropping a
/// candidate; a <c>NoopReranker</c> preserves fused order. Hosts may inject an LLM- or
/// cross-encoder-backed reranker through this interface — the library stays model-free.</para>
/// <para><b>Ranks exactly once:</b> the retrieval pipeline reranks internally; a second
/// pass over already-reranked results compounds boosts (found by audit) — hosts calling
/// <see cref="IReranker.RerankAsync"/> directly own that responsibility.</para>
/// </summary>
public interface IReranker
{
    /// <summary>
    /// Returns the reranked candidates. Implementations must be deterministic for equal
    /// inputs; the returned list may be shorter only when candidates are explicitly dropped.
    /// </summary>
    Task<IReadOnlyList<ScoredMemoryRecord>> RerankAsync(
        RetrievalQuery query,
        IReadOnlyList<ScoredMemoryRecord> candidates,
        CancellationToken cancellationToken = default);
}