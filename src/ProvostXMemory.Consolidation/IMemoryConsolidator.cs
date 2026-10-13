using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Consolidation;

/// <summary>Consolidation pass over a scope: dedup, merge, TTL expiry, cluster summaries.</summary>
public interface IMemoryConsolidator
{
    Task<IReadOnlyList<MemoryRecord>> ConsolidateAsync(string scopeKind, string scopeId, ConsolidationOptions options, CancellationToken cancellationToken = default);
}