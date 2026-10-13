using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Retrieval;

/// <summary>Query across modalities (multi-representation records).</summary>
public interface IMultiRepresentationRetriever
{
    Task<RetrievalResult> RetrieveAsync(RetrievalQuery query, MemoryRepresentationModality modality, CancellationToken cancellationToken = default);
}