using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Consolidation;

/// <summary>Summarizes a cluster of records into a new summary record.</summary>
public interface IClusterSummarizer
{
    Task<MemoryRecord> SummarizeAsync(string scopeKind, string scopeId, IReadOnlyList<MemoryRecord> cluster, CancellationToken cancellationToken = default);
}