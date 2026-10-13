using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.Eval;

using Xunit;

namespace ProvostXMemory.SystemTests.Unit;

/// <summary>
/// EvalReport JSON round-trip through the module's own serializer: all fields survive,
/// nested collections and traces stay intact, threshold violations carry over, and the
/// canonical write form is stable (write(read(x)) == x).
/// </summary>
public sealed class EvalReportRoundTripTests
{
    private static EvalReport SampleReport()
    {
        var aggregate = new EvalMetrics(RecallAtK: 0.85, PrecisionAtK: 0.4, Mrr: 0.9, NdcgAtK: 0.77);
        var cases = new List<EvalCaseMetrics>
        {
            new("case-1", "Session", "s1", 1.0, 0.5, 1.0, 1.0, 5, 3, 3),
            new("case-2", "Session", "s1", 0.7, 0.3, 0.8, 0.55, 5, 2, 3),
        };
        var violations = new MetricThresholdViolations(
            new List<MetricThresholdViolation> { new(EvalMetricThresholds.MetricRecallAtK, 0.85, 0.9) });
        var traces = new List<EvalCaseTrace>
        {
            new("case-1", "Session", "s1", 1, new List<EvalCaseTraceHit>
            {
                new("rec-a", 1.0),
                new("rec-b", 0.0),
            }),
            new("case-2", "Tenant", "t9", 0, new List<EvalCaseTraceHit>
            {
                new("rec-c", 1.0),
            }),
        };

        return new EvalReport(
            SetId: "round-trip-set",
            TopK: 5,
            Aggregate: aggregate,
            Cases: cases,
            PerScope: new[] { new EvalMetricBreakdownEntry("Session/s1", aggregate, new[] { "case-1", "case-2" }) },
            PerStage: new[]
            {
                new EvalMetricBreakdownEntry("dense", new EvalMetrics(0.9, 0.45, 0.95, 0.8), new[] { "case-1" }),
                new EvalMetricBreakdownEntry("lexical", new EvalMetrics(0.8, 0.35, 0.85, 0.7), new[] { "case-2" }),
            },
            ThresholdViolations: violations,
            Traces: traces,
            CorpusRecordCount: 42,
            TotalDurationMs: 123.45);
    }

    [Fact]
    public void Serialize_deserialize_preserves_every_field()
    {
        var report = SampleReport();
        var json = new EvalReportJson();

        var roundTripped = json.Read(json.Write(report));

        Assert.NotNull(roundTripped);
        Assert.Equal(report.SetId, roundTripped.SetId);
        Assert.Equal(report.TopK, roundTripped.TopK);
        Assert.Equal(report.CorpusRecordCount, roundTripped.CorpusRecordCount);
        Assert.Equal(report.TotalDurationMs, roundTripped.TotalDurationMs, precision: 9);

        Assert.Equal(report.Aggregate.RecallAtK, roundTripped.Aggregate.RecallAtK, precision: 12);
        Assert.Equal(report.Aggregate.PrecisionAtK, roundTripped.Aggregate.PrecisionAtK, precision: 12);
        Assert.Equal(report.Aggregate.Mrr, roundTripped.Aggregate.Mrr, precision: 12);
        Assert.Equal(report.Aggregate.NdcgAtK, roundTripped.Aggregate.NdcgAtK, precision: 12);

        Assert.Equal(report.Cases.Count, roundTripped.Cases.Count);
        for (var i = 0; i < report.Cases.Count; i++)
        {
            Assert.Equal(report.Cases[i].CaseId, roundTripped.Cases[i].CaseId);
            Assert.Equal(report.Cases[i].ScopeKind, roundTripped.Cases[i].ScopeKind);
            Assert.Equal(report.Cases[i].ScopeId, roundTripped.Cases[i].ScopeId);
            Assert.Equal(report.Cases[i].ResultCount, roundTripped.Cases[i].ResultCount);
            Assert.Equal(report.Cases[i].RelevantTotalCount, roundTripped.Cases[i].RelevantTotalCount);
            Assert.Equal(report.Cases[i].RecallAtK, roundTripped.Cases[i].RecallAtK, precision: 12);
        }

        Assert.Single(roundTripped.PerScope);
        Assert.Equal("Session/s1", roundTripped.PerScope[0].Key);
        Assert.Equal(2, roundTripped.PerScope[0].CaseIds.Count);
        Assert.Equal(2, roundTripped.PerStage.Count);
        Assert.Equal("dense", roundTripped.PerStage[0].Key);
        Assert.Equal("lexical", roundTripped.PerStage[1].Key);

        Assert.True(roundTripped.ThresholdViolations.HasViolations);
        var violation = Assert.Single(roundTripped.ThresholdViolations.Violations);
        Assert.Equal(EvalMetricThresholds.MetricRecallAtK, violation.Metric);
        Assert.Equal(0.85, violation.Actual, precision: 12);
        Assert.Equal(0.9, violation.Required, precision: 12);

        Assert.Equal(2, roundTripped.Traces.Count);
        Assert.Equal(1, roundTripped.Traces[0].ForeignRecordHits);
        Assert.Equal(2, roundTripped.Traces[0].Hits.Count);
        Assert.Equal("rec-a", roundTripped.Traces[0].Hits[0].RecordId);
        Assert.Equal(1.0, roundTripped.Traces[0].Hits[0].Gain, precision: 12);
        Assert.Equal(0.0, roundTripped.Traces[0].Hits[1].Gain, precision: 12);
        Assert.Equal("rec-c", roundTripped.Traces[1].Hits[0].RecordId);
        Assert.False(roundTripped.AllThresholdsPass);
    }

    [Fact]
    public void Canonical_form_is_stable_across_a_full_cycle()
    {
        var json = new EvalReportJson();
        var first = json.Write(SampleReport());
        var second = json.Write(json.Read(first)!);

        Assert.Equal(first, second); // snake_case, indented output stays canonical
        Assert.Contains("\"recall_at_k\"", first, StringComparison.Ordinal);
        Assert.Contains("\"set_id\"", first, StringComparison.Ordinal);
    }

    [Fact]
    public void Passing_report_serializes_without_violations()
    {
        var report = new EvalReport(
            SetId: "clean",
            TopK: 5,
            Aggregate: new EvalMetrics(1.0, 1.0, 1.0, 1.0),
            Cases: Array.Empty<EvalCaseMetrics>(),
            PerScope: Array.Empty<EvalMetricBreakdownEntry>(),
            PerStage: Array.Empty<EvalMetricBreakdownEntry>(),
            ThresholdViolations: MetricThresholdViolations.None(),
            Traces: Array.Empty<EvalCaseTrace>(),
            CorpusRecordCount: 0,
            TotalDurationMs: 0.0);
        var json = new EvalReportJson();

        var roundTripped = json.Read(json.Write(report))!;

        Assert.False(roundTripped.ThresholdViolations.HasViolations);
        Assert.True(roundTripped.AllThresholdsPass);
        Assert.Empty(roundTripped.Traces);
    }

    [Fact]
    public void Null_payload_reads_as_null()
    {
        Assert.Null(new EvalReportJson().Read("null"));
    }

    [Fact]
    public void Null_arguments_throw()
    {
        var json = new EvalReportJson();

        Assert.Throws<ArgumentNullException>(() => json.Write(null!));
        Assert.Throws<ArgumentNullException>(() => json.Read(null!));
    }
}