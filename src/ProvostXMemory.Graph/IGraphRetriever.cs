using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Graph;

/// <summary>Retrieves records from a relevant subgraph plus cluster summaries.</summary>
public interface IGraphRetriever
{
    Task<IReadOnlyList<ScoredMemoryRecord>> RetrieveAsync(string query, MemoryScope scope, int topK, CancellationToken cancellationToken = default);
}