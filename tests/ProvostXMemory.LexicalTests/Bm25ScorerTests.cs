using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.Lexical;

using Xunit;

namespace ProvostXMemory.LexicalTests;

/// <summary>Hand-computed BM25-lite scorer math on tiny corpora.</summary>
public sealed class Bm25ScorerTests
{
    private const double Precision = 1e-9;

    private static CorpusStatistics BuildStats(
        int documentCount,
        params (string Term, int DocumentFrequency, int TotalOccurrences)[] terms)
    {
        var map = terms.ToDictionary(
            t => t.Term,
            t => new TermStat(t.Term, t.DocumentFrequency, t.TotalOccurrences));
        return new CorpusStatistics(documentCount, map);
    }

    [Fact]
    public void Saturated_term_frequency_times_idf_matches_hand_computation()
    {
        // N=3, "cat": df=1 → idf=ln(1+3/1)=ln(4); tf=2 → 2/(2+1.2) = 0.625.
        var scorer = new Bm25Scorer();
        var score = scorer.Score(
            statistics: BuildStats(3, ("cat", 1, 2)),
            queryTerms: new[] { "cat" },
            documentTerms: new[] { "cat", "cat", "dog" });

        Assert.Equal(Math.Log(4.0) * 0.625, score, Precision);
    }

    [Fact]
    public void Key_match_boost_applies_idf_times_two()
    {
        // N=3, "dog": df=2 → idf=ln(1+3/2)=ln(2.5); key hit only → ln(2.5)*2.
        var scorer = new Bm25Scorer();
        var score = scorer.Score(
            statistics: BuildStats(3, ("dog", 2, 1)),
            queryTerms: new[] { "dog" },
            documentTerms: new[] { "bird" },
            keyTerms: new[] { "dog" });

        Assert.Equal(Math.Log(2.5) * 2.0, score, Precision);
    }

    [Fact]
    public void Key_match_boost_is_overridable()
    {
        var scorer = new Bm25Scorer();
        var score = scorer.Score(
            statistics: BuildStats(3, ("dog", 2, 1)),
            queryTerms: new[] { "dog" },
            documentTerms: Array.Empty<string>(),
            keyTerms: new[] { "dog" },
            keyBoost: 3.0);

        Assert.Equal(Math.Log(2.5) * 3.0, score, Precision);
    }

    [Fact]
    public void Key_hit_plus_body_tf_accumulate()
    {
        // "run": key + body tf=2, df=1, N=3 → ln(4) * (2/3.2 + 2.0).
        var scorer = new Bm25Scorer();
        var score = scorer.Score(
            statistics: BuildStats(3, ("run", 1, 2)),
            queryTerms: new[] { "run" },
            documentTerms: new[] { "run", "run" },
            keyTerms: new[] { "run" });

        Assert.Equal(Math.Log(4.0) * ((2.0 / 3.2) + 2.0), score, Precision);
    }

    [Fact]
    public void Multiple_query_terms_sum_independently()
    {
        // "cat": df=1 → ln(4), tf=1 → 1/2.2; "dog": df=2 → ln(2.5), absent from body, no key.
        var scorer = new Bm25Scorer();
        var score = scorer.Score(
            statistics: BuildStats(3, ("cat", 1, 1), ("dog", 2, 0)),
            queryTerms: new[] { "cat", "dog" },
            documentTerms: new[] { "cat" });

        Assert.Equal(Math.Log(4.0) * (1.0 / 2.2), score, Precision);
    }

    [Fact]
    public void Single_term_query_matching_category_receives_bonus()
    {
        var scorer = new Bm25Scorer();
        var stats = BuildStats(1, ("fact", 1, 1));
        var withCategory = scorer.Score(
            statistics: stats,
            queryTerms: new[] { "fact" },
            documentTerms: new[] { "fact" },
            category: "FACT");
        var withoutCategory = scorer.Score(
            statistics: stats,
            queryTerms: new[] { "fact" },
            documentTerms: new[] { "fact" });

        Assert.Equal(Math.Log(2.0) * (1.0 / 2.2) + 0.5, withCategory, Precision);
        Assert.Equal(Math.Log(2.0) * (1.0 / 2.2), withoutCategory, Precision);
    }

    [Fact]
    public void Multi_term_query_gets_no_category_bonus()
    {
        var scorer = new Bm25Scorer();
        var score = scorer.Score(
            statistics: BuildStats(1, ("fact", 1, 1)),
            queryTerms: new[] { "fact", "other" },
            documentTerms: new[] { "fact" },
            category: "fact");

        Assert.Equal(Math.Log(2.0) * (1.0 / 2.2), score, Precision);
    }

    [Fact]
    public void Unseen_term_has_zero_contribution()
    {
        var scorer = new Bm25Scorer();
        var score = scorer.Score(
            statistics: BuildStats(5, ("known", 1, 1)),
            queryTerms: new[] { "ghost" },
            documentTerms: new[] { "known" });

        Assert.Equal(0.0, score, Precision);
    }

    [Fact]
    public void Empty_query_or_empty_document_score_zero()
    {
        var scorer = new Bm25Scorer();
        var stats = BuildStats(2, ("cat", 1, 1));

        Assert.Equal(0.0, scorer.Score(stats, Array.Empty<string>(), new[] { "cat" }), Precision);
        Assert.Equal(0.0, scorer.Score(stats, new[] { "cat" }, Array.Empty<string>()), Precision);
    }

    [Fact]
    public void Null_key_terms_and_category_are_tolerated()
    {
        var scorer = new Bm25Scorer();
        var score = scorer.Score(
            statistics: BuildStats(3, ("cat", 1, 2)),
            queryTerms: new[] { "cat" },
            documentTerms: new[] { "cat", "cat" },
            keyTerms: null,
            category: null);

        Assert.Equal(Math.Log(4.0) * 0.625, score, Precision);
    }

    [Fact]
    public void Repeated_query_terms_double_their_contribution()
    {
        // Reference BM25-lite behavior: each query-term occurrence adds another component.
        var scorer = new Bm25Scorer();
        var stats = BuildStats(3, ("cat", 1, 1));
        var once = scorer.Score(stats, new[] { "cat" }, new[] { "cat" });
        var twice = scorer.Score(stats, new[] { "cat", "cat" }, new[] { "cat" });

        Assert.Equal(2 * once, twice, Precision);
    }

    [Fact]
    public void Single_document_corpus_scores_ln2_component()
    {
        // N=1, df=1 → idf=ln(2); tf=1 → 1/2.2.
        var scorer = new Bm25Scorer();
        var score = scorer.Score(
            statistics: BuildStats(1, ("solo", 1, 1)),
            queryTerms: new[] { "solo" },
            documentTerms: new[] { "solo" });

        Assert.Equal(Math.Log(2.0) * (1.0 / 2.2), score, Precision);
    }
}