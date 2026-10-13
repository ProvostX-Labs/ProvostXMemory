using System;
using System.Collections.Generic;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Lexical;


/// <summary>
/// BM25-lite scoring: saturated term frequency (k1 = 1.2) × IDF, plus a key-match boost
/// on key-term hits and a single-term category match bonus. Corpus statistics are passed
/// per call (owned by the index, which keeps them current after build/add).
/// </summary>
public interface IBm25Scorer
{
    /// <summary>
    /// Scores <paramref name="documentTerms"/> against <paramref name="queryTerms"/> using
    /// <paramref name="statistics"/>. Key-term hits receive the key boost (default 2.0).
    /// </summary>
    double Score(
        CorpusStatistics statistics,
        IReadOnlyList<string> queryTerms,
        IReadOnlyList<string> documentTerms,
        IReadOnlyList<string>? keyTerms = null,
        string? category = null,
        double keyBoost = 2.0);
}

