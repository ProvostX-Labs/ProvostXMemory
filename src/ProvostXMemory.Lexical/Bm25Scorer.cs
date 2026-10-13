using System;
using System.Collections.Generic;

namespace ProvostXMemory.Lexical;

/// <summary>
/// BM25-lite scoring: saturated term frequency (k1 = 1.2) × IDF plus a key-match boost
/// (default 2.0) on key-term hits and a modest single-term category match bonus (0.5).
/// Classic BM25-lite: no length normalization, no third-party dependencies. Stateless
/// pure scoring — corpus statistics are passed in per call (the index owns them).
/// </summary>
public sealed class Bm25Scorer : IBm25Scorer
{
    private const double K1Saturation = 1.2;
    private const double DefaultKeyBoost = 2.0;
    private const double CategoryBonus = 0.5;

    /// <inheritdoc />
    public double Score(
        CorpusStatistics statistics,
        IReadOnlyList<string> queryTerms,
        IReadOnlyList<string> documentTerms,
        IReadOnlyList<string>? keyTerms = null,
        string? category = null,
        double keyBoost = DefaultKeyBoost)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(queryTerms);
        ArgumentNullException.ThrowIfNull(documentTerms);

        if (queryTerms.Count == 0)
        {
            return 0.0;
        }

        var bodyCounts = LexicalTermCounts.Count(documentTerms);
        var keySet = LexicalTermCounts.Set(keyTerms);

        var documentCount = (double)Math.Max(1, statistics.DocumentCount);
        var score = 0.0;
        foreach (var term in queryTerms)
        {
            var documentFrequency = Math.Max(1, statistics.DocumentFrequency(term));
            var idf = Math.Log(1.0 + documentCount / documentFrequency);
            if (bodyCounts.TryGetValue(term, out var termFrequency) && termFrequency > 0)
            {
                score += idf * (termFrequency / (termFrequency + K1Saturation));
            }

            if (keySet.Contains(term))
            {
                score += idf * keyBoost;
            }
        }

        if (queryTerms.Count == 1 &&
            category is not null &&
            string.Equals(category, queryTerms[0], StringComparison.OrdinalIgnoreCase))
        {
            score += CategoryBonus;
        }

        return score;
    }
}