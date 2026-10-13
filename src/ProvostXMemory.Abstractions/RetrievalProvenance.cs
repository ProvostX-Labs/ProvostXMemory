using System.Collections.Generic;

namespace ProvostXMemory.Abstractions;

/// <summary>Provenance of a scored memory record: which retriever produced it and per-stage scores.</summary>
public sealed record RetrievalProvenance(
    string Retriever,
    IReadOnlyDictionary<string, double>? StageScores = null)
{
    private static readonly IReadOnlyDictionary<string, double> EmptyStageScores =
        new Dictionary<string, double>();

    public IReadOnlyDictionary<string, double> StageScores { get; init; } = StageScores ?? EmptyStageScores;
}