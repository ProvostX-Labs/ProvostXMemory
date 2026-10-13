using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Correction;


/// <summary>Grades candidates for relevance (host-injected).</summary>
public interface IRetrievalGrader
{
    Task<GradedCandidate> GradeAsync(RetrievalQuery query, ScoredMemoryRecord candidate, CancellationToken cancellationToken = default);
}