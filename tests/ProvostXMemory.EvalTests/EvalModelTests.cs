using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Eval;

using Xunit;

namespace ProvostXMemory.EvalTests;

/// <summary>EvalCase / EvalCaseSet validation, graded relevance, scope grouping.</summary>
public sealed class EvalModelTests
{
    private static MemoryScope Scope(string id) => new(MemoryScopeKind.Session, id);

    [Fact]
    public void EvalCase_binary_relevance_defaults_to_one()
    {
        var evalCase = new EvalCase("c1", "q", Scope("s"), new[] { "a", "b" });
        Assert.Equal(1.0, evalCase.RelevanceOf("a"), precision: 9);
        Assert.Equal(1.0, evalCase.RelevanceOf("b"), precision: 9);
        Assert.Equal(0.0, evalCase.RelevanceOf("zzz"), precision: 9);
    }

    [Fact]
    public void EvalCase_graded_relevance_is_returned_verbatim()
    {
        var evalCase = new EvalCase("c1", "q", Scope("s"), new[] { "a", "b" }, new[] { 0.3, 0.9 });
        Assert.Equal(0.3, evalCase.RelevanceOf("a"), precision: 9);
        Assert.Equal(0.9, evalCase.RelevanceOf("b"), precision: 9);
        Assert.Equal(0.0, evalCase.RelevanceOf("c"), precision: 9);
    }

    [Fact]
    public void EvalCase_rejects_bad_arguments()
    {
        var scope = Scope("s");
        Assert.Throws<ArgumentException>(() => new EvalCase("", "q", scope, new[] { "a" }));
        Assert.Throws<ArgumentException>(() => new EvalCase("c", "", scope, new[] { "a" }));
        Assert.Throws<ArgumentException>(() => new EvalCase("c", "q", scope, Array.Empty<string>()));
        Assert.Throws<ArgumentException>(() => new EvalCase("c", " ", scope, new[] { "a" }));
        Assert.Throws<ArgumentException>(() => new EvalCase("c", "q", scope, new[] { "a", "b" }, new[] { 0.5 }));
        Assert.Throws<ArgumentException>(() => new EvalCase("c", "q", scope, new[] { "a" }, new[] { 1.5 }));
        Assert.Throws<ArgumentException>(() => new EvalCase("c", "q", scope, new[] { "a" }, new[] { -0.1 }));
    }

    [Fact]
    public void EvalCaseSet_scopes_listed_first_appearance_order()
    {
        var a = Scope("a");
        var b = Scope("b");
        var set = new EvalCaseSet("set-1", new[]
        {
            new EvalCase("c1", "q1", a, new[] { "r1" }),
            new EvalCase("c2", "q2", b, new[] { "r2" }),
            new EvalCase("c3", "q3", a, new[] { "r3" }),
        });

        Assert.Equal(2, set.Scopes().Count);
        Assert.True(set.Scopes()[0].Equals(a));
        Assert.True(set.Scopes()[1].Equals(b));
        Assert.Equal(3, set.Cases.Count);
    }

    [Fact]
    public void EvalCaseSet_allows_same_case_id_in_different_scopes()
    {
        var set = new EvalCaseSet("set-1", new[]
        {
            new EvalCase("same-id", "q1", Scope("a"), new[] { "r1" }),
            new EvalCase("same-id", "q2", Scope("b"), new[] { "r2" }),
        });

        Assert.Equal(2, set.Cases.Count);
    }

    [Fact]
    public void EvalCaseSet_rejects_duplicate_id_within_scope()
    {
        Assert.Throws<ArgumentException>(() => new EvalCaseSet("set-1", new[]
        {
            new EvalCase("dup", "q1", Scope("a"), new[] { "r1" }),
            new EvalCase("dup", "q2", Scope("a"), new[] { "r2" }),
        }));
    }

    [Fact]
    public void EvalCaseSet_with_cases_appends_and_revalidates()
    {
        var set = new EvalCaseSet("set-1", new[]
        {
            new EvalCase("c1", "q1", Scope("a"), new[] { "r1" }),
        });
        var extended = set.WithCases(new[] { new EvalCase("c2", "q2", Scope("a"), new[] { "r2" }) });

        Assert.Equal(2, extended.Cases.Count);
        Assert.Throws<ArgumentException>(() =>
            extended.WithCases(new[] { new EvalCase("c2", "q3", Scope("a"), new[] { "r3" }) }));
    }

    [Fact]
    public void Threshold_violations_reported_with_actual_and_required()
    {
        var thresholds = new EvalMetricThresholds(recallAtK: 0.9, mrr: 0.5);
        var aggregate = new EvalMetrics(0.75, 0.5, 0.4, 0.9);

        var violations = thresholds.Check(aggregate);

        Assert.True(violations.HasViolations);
        Assert.Equal(2, violations.Violations.Count);
        var recall = violations.Violations.Single(v => v.Metric == EvalMetricThresholds.MetricRecallAtK);
        Assert.Equal(0.75, recall.Actual, precision: 9);
        Assert.Equal(0.9, recall.Required, precision: 9);
        Assert.Equal(0.4, violations.Violations.Single(v => v.Metric == EvalMetricThresholds.MetricMrr).Actual, precision: 9);
    }

    [Fact]
    public void Thresholds_passing_yield_no_violations()
    {
        var thresholds = new EvalMetricThresholds(recallAtK: 0.5);
        var violations = thresholds.Check(new EvalMetrics(1.0, 1.0, 1.0, 1.0));

        Assert.False(violations.HasViolations);
    }

    [Fact]
    public void Threshold_out_of_range_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvalMetricThresholds(recallAtK: 1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvalMetricThresholds(mrr: -0.1));
    }
}
