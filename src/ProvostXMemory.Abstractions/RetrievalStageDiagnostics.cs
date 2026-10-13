using System;
using System.Collections.Generic;

namespace ProvostXMemory.Abstractions;

/// <summary>Diagnostics of one retrieval stage (scores and timing).</summary>
public sealed record RetrievalStageDiagnostics(
    string Stage,
    TimeSpan Duration,
    IReadOnlyDictionary<string, double> Scores)
{
    private static readonly IReadOnlyDictionary<string, double> EmptyScores =
        new Dictionary<string, double>();

    public IReadOnlyDictionary<string, double> Scores { get; init; } = Scores ?? EmptyScores;
}