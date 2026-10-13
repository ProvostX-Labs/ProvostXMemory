using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Fusion;

using Xunit;

namespace ProvostXMemory.FusionTests;

/// <summary>RRF fusion: hand-verified merges, weighting, empty inputs, deterministic ties.</summary>
public sealed class ReciprocalRankFuserTests
{
    private const double Precision = 1e-9;

    private static RankedList List(string source, params string[] ids)
    {
        return new RankedList(
            Source: source,
            Items: ids.Select(id => new ScoredId("session", "s1", id, 0.0)).ToList());
    }

    private static IReadOnlyList<string> Ids(IReadOnlyList<ScoredId> fused)
    {
        return fused.Select(f => f.Id).ToList();
    }

    [Fact]
    public void Two_disjoint_lists_merge_rank_one_items_first_by_weight()
    {
        // k=60: rank1 → 1/61, rank2 → 1/62. Equal weights → ties break by id.
        var fuser = new ReciprocalRankFuser();
        var fused = fuser.Fuse(
            new[]
            {
                List("dense", "B", "C"),
                List("lexical", "A", "D"),
            },
            new FusionOptions());

        // Scores: B=1/61, C=1/62, A=1/61, D=1/62 → id tie order → A,B,C,D.
        Assert.Equal(new[] { "A", "B", "C", "D" }, Ids(fused));
        Assert.Equal(1.0 / 61.0, fused[0].Score, Precision);
        Assert.Equal(1.0 / 61.0, fused[1].Score, Precision);
        Assert.Equal(1.0 / 62.0, fused[2].Score, Precision);
        Assert.Equal(1.0 / 62.0, fused[3].Score, Precision);
    }

    [Fact]
    public void Overlapping_item_ranks_first_by_double_contribution()
    {
        // dense: A,B,C; lexical: A,X,Y → A=2/61 first; B=X=1/62 tie → B,X; C=Y=1/63 tie → C,Y.
        var fuser = new ReciprocalRankFuser();
        var fused = fuser.Fuse(
            new[]
            {
                List("dense", "A", "B", "C"),
                List("lexical", "A", "X", "Y"),
            },
            new FusionOptions());

        Assert.Equal(new[] { "A", "B", "X", "C", "Y" }, Ids(fused));
        Assert.Equal(2.0 / 61.0, fused[0].Score, Precision);
        Assert.Equal(1.0 / 62.0, fused[1].Score, Precision);
        Assert.Equal(1.0 / 62.0, fused[2].Score, Precision);
        Assert.Equal(1.0 / 63.0, fused[3].Score, Precision);
        Assert.Equal(1.0 / 63.0, fused[4].Score, Precision);
    }

    [Fact]
    public void Hand_verified_merge_orders_shared_items_first_then_remaining_by_score()
    {
        // dense: A,B,C,D (ranks 1-4); lexical: B,C,E,F (ranks 1-4).
        // A=1/61 ; B=1/62+1/61 ; C=1/63+1/62 ; D=1/64 ; E=1/63 ; F=1/64.
        var fuser = new ReciprocalRankFuser();
        var fused = fuser.Fuse(
            new[]
            {
                List("dense", "A", "B", "C", "D"),
                List("lexical", "B", "C", "E", "F"),
            },
            new FusionOptions());

        // B(0.03252) > C(0.03200) > A(0.01639) > E(0.01587) > D=F tie (0.015625) → id order D,F.
        Assert.Equal(new[] { "B", "C", "A", "E", "D", "F" }, Ids(fused));
        Assert.Equal(1.0 / 62.0 + 1.0 / 61.0, fused[0].Score, Precision);
        Assert.Equal(1.0 / 63.0 + 1.0 / 62.0, fused[1].Score, Precision);
        Assert.Equal(1.0 / 61.0, fused[2].Score, Precision);
        Assert.Equal(1.0 / 64.0, fused[4].Score, Precision);
    }

    [Fact]
    public void Full_rank_contribution_values_are_exact()
    {
        var fuser = new ReciprocalRankFuser();
        var fused = fuser.Fuse(
            new[] { List("dense", "A", "B", "C") },
            new FusionOptions());

        Assert.Equal(3, fused.Count);
        Assert.Equal(1.0 / 61.0, fused[0].Score, Precision);
        Assert.Equal(1.0 / 62.0, fused[1].Score, Precision);
        Assert.Equal(1.0 / 63.0, fused[2].Score, Precision);
    }

    [Fact]
    public void Custom_rrf_k_changes_contributions()
    {
        var fuser = new ReciprocalRankFuser();
        var fused = fuser.Fuse(
            new[] { List("dense", "A", "B") },
            new FusionOptions(RrfK: 1.0));

        Assert.Equal(1.0 / 2.0, fused[0].Score, Precision);
        Assert.Equal(1.0 / 3.0, fused[1].Score, Precision);
    }

    [Fact]
    public void Lexical_weight_scales_lexical_contributions_only()
    {
        // Lexical rank1 doubled: X = 2/61; B (dense rank1 + lexical rank2) = 1/61 + 2/62;
        // → B(0.04865) > X(0.03279) > A(1/62 dense rank2 = 0.01613).
        var fuser = new ReciprocalRankFuser();
        var fused = fuser.Fuse(
            new[]
            {
                List("dense", "B", "A"),
                List("lexical", "X", "B"),
            },
            new FusionOptions(DenseWeight: 1.0, LexicalWeight: 2.0));

        // X = 2/61 = 0.032787; B = dense rank 1 (1/61) + lexical rank 2 (2/62) = 0.048651.
        Assert.Equal(new[] { "B", "X", "A" }, Ids(fused));
        Assert.Equal(1.0 / 61.0 + 2.0 / 62.0, fused[0].Score, Precision);
        Assert.Equal(2.0 / 61.0, fused[1].Score, Precision);
        Assert.Equal(1.0 / 62.0, fused[2].Score, Precision);
    }

    [Fact]
    public void Dense_weight_zero_suppresses_dense_contributions()
    {
        var fuser = new ReciprocalRankFuser();
        var fused = fuser.Fuse(
            new[]
            {
                List("dense", "A", "B"),
                List("lexical", "X", "Y"),
            },
            new FusionOptions(DenseWeight: 0.0, LexicalWeight: 1.0));

        // Dense items still surface (RRF is rank-based) but with score 0, after lexical items.
        Assert.Equal(new[] { "X", "Y", "A", "B" }, Ids(fused));
        Assert.Equal(0.0, fused[2].Score, Precision);
    }

    [Fact]
    public void Dense_weight_heavily_favors_dense_list()
    {
        // Dense weight 5.0: dense rank1 = 5/61 ≈ 0.08197 > lexical rank1 + dense rank2… verify exact.
        var fuser = new ReciprocalRankFuser();
        var fused = fuser.Fuse(
            new[]
            {
                List("dense", "A", "B"),
                List("lexical", "X", "A"),
            },
            new FusionOptions(DenseWeight: 5.0, LexicalWeight: 1.0));

        // A = 5/61 + 1/62 ≈ 0.09810 ; X = 1/61 ≈ 0.01639 ; B = 5/62 ≈ 0.08065.
        Assert.Equal(new[] { "A", "B", "X" }, Ids(fused));
        Assert.Equal(5.0 / 61.0 + 1.0 / 62.0, fused[0].Score, Precision);
        Assert.Equal(5.0 / 62.0, fused[1].Score, Precision);
        Assert.Equal(1.0 / 61.0, fused[2].Score, Precision);
    }

    [Fact]
    public void Equal_score_ties_break_by_id_then_rank_deterministically()
    {
        // Distinct items at the same rank with equal weights → equal scores → id tie-break.
        var fuser = new ReciprocalRankFuser();
        var first = fuser.Fuse(
            new[]
            {
                List("dense", "zeta", "alpha"),
                List("lexical", "mid"),
            },
            new FusionOptions());
        var second = fuser.Fuse(
            new[]
            {
                List("dense", "zeta", "alpha"),
                List("lexical", "mid"),
            },
            new FusionOptions());

        // zeta=1/61 (dense rank1), mid=1/61 (lexical rank1), alpha=1/62 (dense rank2).
        // Equal scores 1/61 → id order mid,zeta; alpha last.
        Assert.Equal(new[] { "mid", "zeta", "alpha" }, Ids(first));
        Assert.Equal(Ids(first), Ids(second));
    }

    [Fact]
    public void Same_id_in_both_lists_uses_best_rank_for_ties()
    {
        var fuser = new ReciprocalRankFuser();
        var fused = fuser.Fuse(
            new[]
            {
                List("dense", "m", "x"),
                List("lexical", "m"),
            },
            new FusionOptions());

        // m: 2/61; x: 1/62 → m first.
        Assert.Equal(new[] { "m", "x" }, Ids(fused));
        Assert.Equal(2.0 / 61.0, fused[0].Score, Precision);
    }

    [Fact]
    public void Repeated_calls_return_identical_results()
    {
        var fuser = new ReciprocalRankFuser();
        var lists = new[]
        {
            List("dense", "a", "b", "c"),
            List("lexical", "c", "a", "d"),
        };

        var options = new FusionOptions();
        var run1 = fuser.Fuse(lists, options);
        var run2 = fuser.Fuse(lists, options);

        Assert.Equal(Ids(run1), Ids(run2));
        Assert.Equal(run1.Select(f => f.Score), run2.Select(f => f.Score));
    }

    [Fact]
    public void Empty_list_collection_returns_empty()
    {
        var fuser = new ReciprocalRankFuser();
        var fused = fuser.Fuse(Array.Empty<RankedList>(), new FusionOptions());

        Assert.Empty(fused);
    }

    [Fact]
    public void Lists_with_no_items_are_ignored()
    {
        var fuser = new ReciprocalRankFuser();
        var fused = fuser.Fuse(
            new[]
            {
                List("dense"),
                List("lexical", "A"),
            },
            new FusionOptions());

        Assert.Single(fused);
        Assert.Equal("A", fused[0].Id);
        Assert.Equal(1.0 / 61.0, fused[0].Score, Precision);
    }

    [Fact]
    public void Rank_starts_at_one_not_zero()
    {
        var fuser = new ReciprocalRankFuser();
        var fused = fuser.Fuse(
            new[] { List("dense", "A") },
            new FusionOptions(RrfK: 0.0));

        // k=0, rank 1 → 1/(0+1) = 1.
        Assert.Equal(1.0, fused[0].Score, Precision);
    }

    [Fact]
    public void Unknown_source_uses_default_weight_one()
    {
        var fuser = new ReciprocalRankFuser();
        var fused = fuser.Fuse(
            new[] { List("rerank", "A", "B") },
            new FusionOptions(DenseWeight: 0.0, LexicalWeight: 0.0));

        // Unknown sources ignore the dense/lexical weights and weigh 1.0.
        Assert.Equal(1.0 / 61.0, fused[0].Score, Precision);
        Assert.Equal(1.0 / 62.0, fused[1].Score, Precision);
    }

    [Fact]
    public void Fused_scored_id_carries_first_hit_provenance()
    {
        var fuser = new ReciprocalRankFuser();
        var fused = fuser.Fuse(
            new[]
            {
                new RankedList("dense", new[] { new ScoredId("tenant", "t1", "A", 0.9) }),
                new RankedList("lexical", new[] { new ScoredId("session", "s1", "A", 0.5) }),
            },
            new FusionOptions());

        Assert.Equal("tenant", fused[0].ScopeKind);
        Assert.Equal("t1", fused[0].ScopeId);
        Assert.Equal(2.0 / 61.0, fused[0].Score, Precision);
    }

    [Fact]
    public void Null_lists_or_options_throw()
    {
        var fuser = new ReciprocalRankFuser();

        Assert.Throws<ArgumentNullException>(() => fuser.Fuse(null!, new FusionOptions()));
        Assert.Throws<ArgumentNullException>(() => fuser.Fuse(Array.Empty<RankedList>(), null!));
    }
}