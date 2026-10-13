using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Reranking;

using Xunit;

namespace ProvostXMemory.RerankingTests;

/// <summary>
/// <see cref="ScoreBoostReranker"/> ordering fixtures: tag/key/category boosts, length
/// penalties, tie stability, provenance, determinism.
/// </summary>
public sealed class ScoreBoostRerankerTests
{
    [Fact]
    public async Task Tag_match_boost_promotes_candidate()
    {
        // A: no tag match, 0.50. B: tag "redis" matches query term → +0.15 → 0.55.
        var a = Fixtures.Rec("A", 0.50);
        var b = Fixtures.Rec("B", 0.50, tags: new[] { "redis", "cache" });
        var reranker = new ScoreBoostReranker();

        var reranked = await reranker.RerankAsync(Fixtures.Query("redis"), new[] { a, b });

        Assert.Equal(new[] { "B", "A" }, reranked.Select(r => r.Record.Id));
        Assert.Equal(0.65, reranked[0].Score, 9); // 0.50 + 0.15 (only "redis" is a query term)
    }

    [Fact]
    public async Task Multiple_query_terms_accumulate_tag_boosts()
    {
        var a = Fixtures.Rec("A", 0.40, tags: new[] { "redis", "cluster" });
        var reranker = new ScoreBoostReranker();

        var reranked = await reranker.RerankAsync(Fixtures.Query("redis cluster"), new[] { a });

        Assert.Equal(0.70, reranked[0].Score, 9); // 0.40 + 0.15 + 0.15
    }

    [Fact]
    public async Task Key_boost_applies_when_query_term_appears_in_key()
    {
        var a = Fixtures.Rec("A", 0.50, key: "redis-config");
        var b = Fixtures.Rec("B", 0.50, key: "plain-note");
        var reranker = new ScoreBoostReranker();

        var reranked = await reranker.RerankAsync(Fixtures.Query("redis config"), new[] { a, b });

        Assert.Equal(new[] { "A", "B" }, reranked.Select(r => r.Record.Id));
        Assert.Equal(0.70, reranked[0].Score, 9); // 0.50 + 0.10 + 0.10 ("redis" and "config" both in key)
    }

    [Fact]
    public async Task Category_match_boosts_once()
    {
        var a = Fixtures.Rec("A", 0.50, category: "infrastructure");
        var reranker = new ScoreBoostReranker();

        var reranked = await reranker.RerankAsync(Fixtures.Query("infrastructure"), new[] { a });

        Assert.Equal(0.55, reranked[0].Score, 9);
    }

    [Fact]
    public async Task Stopwords_in_query_produce_no_boosts()
    {
        var a = Fixtures.Rec("A", 0.50, tags: new[] { "the", "and" });
        var reranker = new ScoreBoostReranker();

        var reranked = await reranker.RerankAsync(Fixtures.Query("the and of"), new[] { a });

        Assert.Equal(0.50, reranked[0].Score, 9);
    }

    [Fact]
    public async Task Extremely_short_content_is_demoted()
    {
        var shortA = Fixtures.Rec("SHORT", 0.50, content: "abc");
        var normal = Fixtures.Rec("NORMAL", 0.50);
        var reranker = new ScoreBoostReranker();

        var reranked = await reranker.RerankAsync(Fixtures.Query("nothing"), new[] { shortA, normal });

        Assert.Equal(new[] { "NORMAL", "SHORT" }, reranked.Select(r => r.Record.Id));
        Assert.Equal(0.25, reranked[1].Score, 9); // 0.50 − 0.25
    }

    [Fact]
    public async Task Extremely_long_content_is_demoted()
    {
        var long_ = Fixtures.Rec("LONG", 0.50, content: new string('x', 24));
        var normal = Fixtures.Rec("NORMAL", 0.50);
        var reranker = new ScoreBoostReranker(new ScoreBoostRerankerOptions(MaxLength: 12));

        var reranked = await reranker.RerankAsync(Fixtures.Query("nothing"), new[] { long_, normal });

        Assert.Equal(new[] { "NORMAL", "LONG" }, reranked.Select(r => r.Record.Id));
    }

    [Fact]
    public async Task Boost_cannot_overcome_large_score_gap_unless_cumulative()
    {
        var lowButBoosted = Fixtures.Rec("LOW", 0.10, tags: new[] { "redis" });
        var high = Fixtures.Rec("HIGH", 0.90);
        var reranker = new ScoreBoostReranker();

        var reranked = await reranker.RerankAsync(Fixtures.Query("redis"), new[] { lowButBoosted, high });

        Assert.Equal(new[] { "HIGH", "LOW" }, reranked.Select(r => r.Record.Id));
        Assert.Equal(0.25, reranked[1].Score, 9);
    }

    [Fact]
    public async Task Ties_break_by_original_rank()
    {
        var a = Fixtures.Rec("A", 0.50);
        var b = Fixtures.Rec("B", 0.50);
        var c = Fixtures.Rec("C", 0.50);
        var reranker = new ScoreBoostReranker();

        var reranked = await reranker.RerankAsync(Fixtures.Query("nothing"), new[] { a, b, c });

        Assert.Equal(new[] { "A", "B", "C" }, reranked.Select(r => r.Record.Id));
    }

    [Fact]
    public async Task No_matches_leave_order_and_scores_unchanged()
    {
        var a = Fixtures.Rec("A", 0.70);
        var b = Fixtures.Rec("B", 0.30);
        var reranker = new ScoreBoostReranker();

        var reranked = await reranker.RerankAsync(Fixtures.Query("zzz-unrelated"), new[] { a, b });

        Assert.Equal(new[] { "A", "B" }, reranked.Select(r => r.Record.Id));
        Assert.Equal(0.70, reranked[0].Score, 9);
        Assert.Equal(0.30, reranked[1].Score, 9);
    }

    [Fact]
    public async Task Provenance_reports_reranker_name_and_stage_scores()
    {
        var a = Fixtures.Rec("A", 0.50, tags: new[] { "redis" });
        var reranker = new ScoreBoostReranker();

        var reranked = await reranker.RerankAsync(Fixtures.Query("redis"), new[] { a });

        Assert.StartsWith("rerank.score-boost", reranked[0].Provenance.Retriever);
        Assert.Equal(0.50, reranked[0].Provenance.StageScores["fused"], 9);
        Assert.Equal(0.65, reranked[0].Provenance.StageScores["reranked"], 9);
    }

    [Fact]
    public async Task Deterministic_across_repeated_calls_and_instances()
    {
        var candidates = new[]
        {
            Fixtures.Rec("A", 0.50, tags: new[] { "redis" }),
            Fixtures.Rec("B", 0.20, key: "redis-index"),
            Fixtures.Rec("C", 0.80),
        };
        var query = Fixtures.Query("redis index");
        var first = new ScoreBoostReranker();
        var second = new ScoreBoostReranker();

        var r1 = await first.RerankAsync(query, candidates);
        var r2 = await first.RerankAsync(query, candidates);
        var r3 = await second.RerankAsync(query, candidates);

        Assert.Equal(r1.Select(x => x.Record.Id), r2.Select(x => x.Record.Id));
        Assert.Equal(r1.Select(x => x.Record.Id), r3.Select(x => x.Record.Id));
        Assert.Equal(r1.Select(x => x.Score), r3.Select(x => x.Score));
    }

    [Fact]
    public async Task Custom_options_change_boosts_and_thresholds()
    {
        var a = Fixtures.Rec("A", 0.50, tags: new[] { "redis" });
        var reranker = new ScoreBoostReranker(new ScoreBoostRerankerOptions(TagMatchBoost: 1.0));

        var reranked = await reranker.RerankAsync(Fixtures.Query("redis"), new[] { a });

        Assert.Equal(1.50, reranked[0].Score, 9);
    }

    [Fact]
    public async Task Empty_candidates_yield_empty_result()
    {
        var reranker = new ScoreBoostReranker();

        var reranked = await reranker.RerankAsync(
            Fixtures.Query("query"),
            Array.Empty<ScoredMemoryRecord>());

        Assert.Empty(reranked);
    }

    [Fact]
    public async Task Null_arguments_throw()
    {
        var reranker = new ScoreBoostReranker();
        var candidates = new[] { Fixtures.Rec("A", 0.5) };

        await Assert.ThrowsAsync<ArgumentNullException>(() => reranker.RerankAsync(null!, candidates));
        await Assert.ThrowsAsync<ArgumentNullException>(() => reranker.RerankAsync(Fixtures.Query("q"), null!));
    }
}