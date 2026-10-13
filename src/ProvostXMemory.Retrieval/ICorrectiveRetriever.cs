using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Retrieval;

/// <summary>Grader-gated hybrid retrieval with rewrite loop (corrective).</summary>
public interface ICorrectiveRetriever
{
    Task<RetrievalResult> RetrieveAsync(RetrievalQuery query, CancellationToken cancellationToken = default);
}