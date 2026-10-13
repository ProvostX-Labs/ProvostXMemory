using System;
using System.Collections.Generic;

using ProvostXMemory.Eval;

using Xunit;

namespace ProvostXMemory.EvalTests;

/// <summary>
/// Metric formulas verified against hand-computed values (no search engine involved):
/// rankings are plain string lists; expected numbers below were computed by hand.
/// </summary>
public sealed class EvalMetricMathTests
{
    private readonly EvalMetricMath _math = new();

    [Fact]
    public void Recall_all_relevant_in_top_k()
    {
        // relevant {a,b,c}; ranked [a,b,c,...]; k=10 → 3/3 = 1.0
        var value = _math.RecallAtK(Seq("a", "b", "c", "x", "y"), Seq("a", "b", "c"), 10);
        Assert.Equal(1.0, value, precision: 9);
    }

    [Fact]
    public void Recall_partial_hit_counts_fraction()
    {
        // relevant {a,b,c}; top-10 contains only a → 1/3
        var value = _math.RecallAtK(Seq("a", "x", "y", "z", "q", "w", "e", "r", "t", "u"), Seq("a", "b", "c"), 10);
        Assert.Equal(1.0 / 3.0, value, precision: 9);
    }

    [Fact]
    public void Recall_k_cuts_below_relevant_count()
    {
        // relevant {a,b,c}; k=2 window sees b only → 1/3
        var value = _math.RecallAtK(Seq("x", "b", "a", "c"), Seq("a", "b", "c"), 2);
        Assert.Equal(1.0 / 3.0, value, precision: 9);
    }

    [Fact]
    public void Recall_empty_relevant_is_zero()
    {
        Assert.Equal(0.0, _math.RecallAtK(Seq("a"), Array.Empty<string>(), 5), precision: 9);
    }

    [Fact]
    public void Precision_counts_unfilled_slots_against()
    {
        // k=4, hits a only among 2 returned → 1/4 (unfilled slots count)
        var value = _math.PrecisionAtK(Seq("a", "x"), Seq("a", "b", "c"), 4);
        Assert.Equal(0.25, value, precision: 9);
    }

    [Fact]
    public void Precision_full_house()
    {
        var value = _math.PrecisionAtK(Seq("a", "b", "c"), Seq("a", "b"), 3);
        Assert.Equal(2.0 / 3.0, value, precision: 9);
    }

    [Fact]
    public void Mrr_first_relevant_at_rank_2()
    {
        var value = _math.Mrr(Seq("x", "a", "b"), Seq("a", "b"), 10);
        Assert.Equal(0.5, value, precision: 9);
    }

    [Fact]
    public void Mrr_no_relevant_hit_is_zero()
    {
        var value = _math.Mrr(Seq("x", "y", "z"), Seq("a"), 3);
        Assert.Equal(0.0, value, precision: 9);
    }

    [Fact]
    public void Mrr_rank_capped_by_k()
    {
        // relevant at rank 4 but k=3 → 0
        var value = _math.Mrr(Seq("x", "y", "z", "a"), Seq("a"), 3);
        Assert.Equal(0.0, value, precision: 9);
    }

    [Fact]
    public void Ndcg_perfect_ranking_is_one()
    {
        var value = _math.NdcgAtK(Seq("a", "b", "x"), Seq("a", "b"), 3);
        Assert.Equal(1.0, value, precision: 9);
    }

    [Fact]
    public void Ndcg_hand_computed_discounted_hit()
    {
        // ranked [x,a,b]; relevant {a,b}; k=3
        // DCG = 1/log2(3) + 1/log2(4) = 0.630929754 + 0.5 = 1.130929754
        // IDCG = 1/log2(2) + 1/log2(3) = 1 + 0.630929754 = 1.630929754
        // hand: 1.130929754/1.630929754 = 0.693426404
        var value = _math.NdcgAtK(Seq("x", "a", "b"), Seq("a", "b"), 3);
        Assert.Equal(0.693426404, value, precision: 8);
    }

    [Fact]
    public void Ndcg_idcg_capped_at_k()
    {
        // ranked [x]; relevant {a,b,c}; k=1 → DCG=0 → NDCG=0
        var value = _math.NdcgAtK(Seq("x"), Seq("a", "b", "c"), 1);
        Assert.Equal(0.0, value, precision: 9);
    }

    [Fact]
    public void Ndcg_graded_hand_computed()
    {
        // ranked [a,b,x]; gains a=0.8, b=0.3; k=3
        // DCG = 0.8/log2(2) + 0.3/log2(3) = 0.8 + 0.189274258 = 0.989274258
        // IDCG = 0.8/1 + 0.3/log2(3) = 0.989274258 (ideal order == result order)
        var value = _math.NdcgAtKGraded(Seq("a", "b", "x"), Graded(0.8, 0.3, 0.0), 3);
        Assert.Equal(1.0, value, precision: 9);
    }

    [Fact]
    public void Ndcg_graded_prefers_high_gain_first()
    {
        // ranked [a,b]; gains a=0.3, b=0.9; k=2
        // DCG = 0.3/log2(2) + 0.9/log2(3) = 0.3 + 0.567616297 = 0.867616297
        // IDCG = 0.9/log2(2) + 0.3/log2(3) = 0.9 + 0.189274259 = 1.089274259
        // hand: DCG 0.867837/IDCG 1.089279 = 0.796708 (checked against reference values)
        var value = _math.NdcgAtKGraded(Seq("a", "b"), Graded(0.3, 0.9), 2);
        Assert.Equal(0.796708, value, precision: 5);
    }

    [Fact]
    public void Evaluate_case_binds_scope_and_counts()
    {
        var scope = new ProvostXMemory.Abstractions.MemoryScope(
            ProvostXMemory.Abstractions.MemoryScopeKind.Session, "s1");
        var evalCase = new EvalCase("case-1", "query", scope, Seq("a", "b"));
        var metrics = _math.EvaluateCase(evalCase, Seq("a", "x"), topK: 2, relevantRetrievedCount: 1);

        Assert.Equal("case-1", metrics.CaseId);
        Assert.Equal("Session", metrics.ScopeKind);
        Assert.Equal("s1", metrics.ScopeId);
        // recall = 1/2; precision = 1/2; mrr = 1; ndcg = 1
        Assert.Equal(0.5, metrics.RecallAtK, precision: 9);
        Assert.Equal(0.5, metrics.PrecisionAtK, precision: 9);
        Assert.Equal(1.0, metrics.Mrr, precision: 9);
        Assert.Equal(1, metrics.RelevantRetrievedCount);
        Assert.Equal(2, metrics.RelevantTotalCount);
        // MRR with a in rank 1 = 1.0; NDCG = DCG 1/log2(2) / IDCG (1/log2(2) + 1/log2(3))
        Assert.Equal(1.0 / (1.0 + 1.0 / Math.Log2(3)), metrics.NdcgAtK, precision: 9);
    }

    [Fact]
    public void Mean_of_cases_averages_each_metric()
    {
        var scope = new ProvostXMemory.Abstractions.MemoryScope(
            ProvostXMemory.Abstractions.MemoryScopeKind.Session, "s1");
        var first = new EvalCaseMetrics("c1", "Session", "s1", 1.0, 0.5, 1.0, 1.0, 2, 2, 2);
        var second = new EvalCaseMetrics("c2", "Session", "s1", 0.0, 0.0, 0.5, 0.5, 2, 0, 2);
        var mean = EvalMetrics.Mean(new[] { first, second });

        Assert.Equal(0.5, mean.RecallAtK, precision: 9);
        Assert.Equal(0.25, mean.PrecisionAtK, precision: 9);
        Assert.Equal(0.75, mean.Mrr, precision: 9);
        Assert.Equal(0.75, mean.NdcgAtK, precision: 9);
    }

    [Fact]
    public void Mean_empty_is_all_zero()
    {
        var mean = EvalMetrics.Mean(Array.Empty<EvalCaseMetrics>());
        Assert.Equal(0.0, mean.RecallAtK, precision: 9);
        Assert.Equal(0.0, mean.NdcgAtK, precision: 9);
    }

    [Fact]
    public void Invalid_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => _math.RecallAtK(null!, Seq("a"), 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => _math.RecallAtK(Seq("a"), Seq("a"), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _math.PrecisionAtK(Seq("a"), Seq("a"), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _math.Mrr(Seq("a"), Seq("a"), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _math.NdcgAtK(Seq("a"), Seq("a"), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _math.NdcgAtKGraded(Seq("a"), Graded(1.0), 0));
    }

    private static IReadOnlyList<string> Seq(params string[] items) => items;

    private static IReadOnlyList<double> Graded(params double[] gains) => gains;
}
