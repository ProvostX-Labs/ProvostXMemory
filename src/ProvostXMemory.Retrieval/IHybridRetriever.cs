using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Retrieval;

/// <summary>Dense + lexical retrieval in parallel → fused results.</summary>
public interface IHybridRetriever
{
    Task<RetrievalResult> RetrieveAsync(RetrievalQuery query, CancellationToken cancellationToken = default);
}