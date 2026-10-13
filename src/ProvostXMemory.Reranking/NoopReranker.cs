using System;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Reranking;

/// <summary>Identity reranker: returns candidates unchanged (deterministic pass-through).</summary>
public sealed class NoopReranker : IReranker
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ScoredMemoryRecord>> RerankAsync(
        RetrievalQuery query,
        IReadOnlyList<ScoredMemoryRecord> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidates);
        return Task.FromResult(candidates);
    }
}