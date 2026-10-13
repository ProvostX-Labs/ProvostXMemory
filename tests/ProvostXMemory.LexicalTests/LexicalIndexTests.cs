using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.Lexical;

using Xunit;

namespace ProvostXMemory.LexicalTests;

/// <summary>Inverted-index behavior: build, incremental add, deterministic search.</summary>
public sealed class LexicalIndexTests
{
    private static LexicalDocument Doc(
        string id,
        string content,
        string? key = null,
        string? category = null)
    {
        return new LexicalDocument(
            Id: id,
            BodyTerms: LexicalTokenizer.Tokenize(content).ToList(),
            KeyTerms: LexicalTokenizer.Tokenize(key).ToList(),
            Category: category);
    }

    [Fact]
    public void Search_ranks_exact_key_match_above_body_only_match()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[]
        {
            Doc("d1", "cat sits on the mat quietly waiting", key: "cat"),
            Doc("d2", "cat and dog share the garden", key: "dog"),
            Doc("d3", "dog barks at night alone", key: "dog"),
        });

        var hits = index.Search(new[] { "cat" }, 10);

        Assert.Equal(2, hits.Count);
        Assert.Equal("d1", hits[0].Id);
        Assert.Equal("d2", hits[1].Id);
        Assert.True(hits[0].Score > 0.0);
        Assert.True(hits[1].Score > 0.0);
    }

    [Fact]
    public void Key_only_document_is_surfaced_when_body_lacks_all_query_terms()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[]
        {
            Doc("body", "unrelated words entirely", key: "dog"),
            Doc("other", "more filler words", key: "bird"),
        });

        var hits = index.Search(new[] { "dog" }, 10);

        Assert.Single(hits);
        Assert.Equal("body", hits[0].Id);
        Assert.True(hits[0].Score > 0.0);
    }

    [Fact]
    public void Saturated_tf_beats_single_occurrence_across_documents()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[]
        {
            Doc("d1", "cat cat cat plays"),
            Doc("d2", "cat naps"),
        });

        var hits = index.Search(new[] { "cat" }, 10);

        Assert.Equal(2, hits.Count);
        Assert.Equal("d1", hits[0].Id);

        // Saturation: 3 occurrences score below 3x a single occurrence.
        var one = hits.First(h => h.Id == "d2").Score;
        var three = hits.First(h => h.Id == "d1").Score;
        Assert.True(three < 3 * one);
        Assert.True(three > one);
    }

    [Fact]
    public void Rare_term_scores_higher_than_common_term()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[]
        {
            Doc("d1", "quantum ferret box"),
            Doc("d2", "box storage box"),
            Doc("d3", "another box item"),
        });

        // "quantum" appears in 1 document, "box" in 3 → idf(quantum) > idf(box).
        var quantumHits = index.Search(new[] { "quantum" }, 10);
        var boxHits = index.Search(new[] { "box" }, 10);

        Assert.Single(quantumHits);
        Assert.Equal(3, boxHits.Count);
        Assert.True(quantumHits[0].Score > boxHits[0].Score);
    }

    [Fact]
    public void Ordering_is_deterministic_with_equal_scores_by_id_ascending()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[]
        {
            Doc("z-doc", "shared term"),
            Doc("a-doc", "shared term"),
            Doc("m-doc", "shared term"),
        });

        var first = index.Search(new[] { "shared" }, 10);
        var second = index.Search(new[] { "shared" }, 10);

        Assert.Equal(new[] { "a-doc", "m-doc", "z-doc" }, first.Select(h => h.Id).ToArray());
        Assert.Equal(first.Select(h => h.Id), second.Select(h => h.Id));
        Assert.Equal(first.Select(h => h.Score), second.Select(h => h.Score));
    }

    [Fact]
    public void TopK_limits_result_count()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[]
        {
            Doc("d1", "common token"),
            Doc("d2", "common token"),
            Doc("d3", "common token"),
            Doc("d4", "common token"),
        });

        var hits = index.Search(new[] { "common" }, 2);

        Assert.Equal(2, hits.Count);
        Assert.Equal(new[] { "d1", "d2" }, hits.Select(h => h.Id).ToArray());
    }

    [Fact]
    public void Non_positive_topK_falls_back_to_default()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[]
        {
            Doc("d1", "term one"),
            Doc("d2", "term two"),
        });

        var hits = index.Search(new[] { "term" }, 0);

        Assert.Equal(2, hits.Count);
    }

    [Fact]
    public void Empty_query_returns_no_hits()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[] { Doc("d1", "anything at all") });

        Assert.Empty(index.Search(Array.Empty<string>(), 10));
    }

    [Fact]
    public void Empty_index_returns_no_hits()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);

        Assert.Empty(index.Search(new[] { "anything" }, 10));
        Assert.Equal(0, index.Count);
    }

    [Fact]
    public void No_matching_terms_returns_no_hits()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[] { Doc("d1", "cats and dogs") });

        var hits = index.Search(new[] { "fish" }, 10);
        Assert.Empty(hits);
    }

    [Fact]
    public void Documents_with_no_terms_at_all_never_match()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[] { Doc("d-empty", "!!!"), Doc("d-full", "word") });

        Assert.DoesNotContain(index.Search(new[] { "word" }, 10), h => h.Id == "d-empty");
        Assert.Equal(2, index.Count);
    }

    [Fact]
    public void Incremental_add_extends_search_results()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[] { Doc("d1", "cat on mat") });

        Assert.Empty(index.Search(new[] { "ferret" }, 10));

        index.IncrementalAdd(Doc("d2", "ferret in cage"));

        var hits = index.Search(new[] { "ferret" }, 10);
        Assert.Single(hits);
        Assert.Equal("d2", hits[0].Id);
        Assert.Equal(2, index.Count);
    }

    [Fact]
    public void Incremental_add_rejects_duplicate_ids()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[] { Doc("d1", "one") });

        Assert.Throws<ArgumentException>(() => index.IncrementalAdd(Doc("d1", "two")));
    }

    [Fact]
    public void Build_rejects_duplicate_ids()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);

        Assert.Throws<ArgumentException>(() => index.Build(new[]
        {
            Doc("dup", "one"),
            Doc("dup", "two"),
        }));
    }

    [Fact]
    public void Build_replaces_previous_contents()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[] { Doc("d1", "old content") });
        index.Build(new[] { Doc("d2", "new content") });

        Assert.Empty(index.Search(new[] { "old" }, 10));
        Assert.Single(index.Search(new[] { "new" }, 10));
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public void Statistics_reflect_document_frequencies_and_counts()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[]
        {
            Doc("d1", "cat cat dog"),
            Doc("d2", "dog bird"),
        });

        Assert.Equal(2, index.Statistics.DocumentCount);
        Assert.Equal(2, index.Statistics.DocumentFrequency("dog"));
        Assert.Equal(2, index.Statistics.TotalOccurrences("dog"));
        Assert.Equal(1, index.Statistics.DocumentFrequency("cat"));
        Assert.Equal(2, index.Statistics.TotalOccurrences("cat"));
        Assert.Equal(0, index.Statistics.DocumentFrequency("ghost"));
    }

    [Fact]
    public void Statistics_count_key_term_document_occurrences()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[]
        {
            Doc("d1", "body words", key: "special"),
            Doc("d2", "more words special", key: "special"),
        });

        // Both documents contain "special" (body or key) → df=2; total occurrences = 1 (body only).
        Assert.Equal(2, index.Statistics.DocumentFrequency("special"));
        Assert.Equal(1, index.Statistics.TotalOccurrences("special"));
    }

    [Fact]
    public void Category_match_surfaces_document_without_body_hit()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[]
        {
            Doc("d-cat", "totally unrelated words", category: "preferences"),
            Doc("d-other", "also unrelated filler", category: "schedule"),
        });

        var hits = index.Search(new[] { "preferences" }, 10);

        Assert.Single(hits);
        Assert.Equal("d-cat", hits[0].Id);
        Assert.Equal(0.5, hits[0].Score, 1e-9);
    }

    [Fact]
    public void Category_match_requires_single_term_query()
    {
        var scorer = new Bm25Scorer();
        var index = new LexicalIndex(scorer);
        index.Build(new[] { Doc("d-cat", "unrelated words", category: "preferences") });

        Assert.Empty(index.Search(new[] { "preferences", "extra" }, 10));
    }
}