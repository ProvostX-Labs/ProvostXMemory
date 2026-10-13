using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Correction;


/// <summary>A grade assigned to one candidate.</summary>
public sealed record GradedCandidate(
    ScoredMemoryRecord Candidate,
    RetrievalGrade Grade);

