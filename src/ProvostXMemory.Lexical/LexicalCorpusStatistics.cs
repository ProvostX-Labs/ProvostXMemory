using System;
using System.Collections.Generic;

namespace ProvostXMemory.Lexical;


/// <summary>Immutable corpus-level statistics: document count plus per-term stats.</summary>
public sealed record CorpusStatistics(
    int DocumentCount,
    IReadOnlyDictionary<string, TermStat> Terms)
{
    private static readonly IReadOnlyDictionary<string, TermStat> EmptyTerms =
        new Dictionary<string, TermStat>();

    public static CorpusStatistics Empty { get; } = new CorpusStatistics(0, EmptyTerms);

    /// <summary>Document frequency of <paramref name="term"/> (0 when unseen).</summary>
    public int DocumentFrequency(string term)
    {
        return Terms.TryGetValue(term, out var stat) ? stat.DocumentFrequency : 0;
    }

    /// <summary>Total occurrences of <paramref name="term"/> across all documents (0 when unseen).</summary>
    public int TotalOccurrences(string term)
    {
        return Terms.TryGetValue(term, out var stat) ? stat.TotalOccurrences : 0;
    }
}