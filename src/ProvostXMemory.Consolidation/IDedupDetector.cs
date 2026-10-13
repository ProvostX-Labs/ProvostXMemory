using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Consolidation;

/// <summary>Near-duplicate detection via similarity threshold.</summary>
public interface IDedupDetector
{
    Task<bool> IsDuplicateAsync(MemoryRecord left, MemoryRecord right, double threshold, CancellationToken cancellationToken = default);
}