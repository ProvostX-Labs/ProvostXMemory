using System;
using System.Collections.Generic;
using System.Linq;

namespace ProvostXMemory.Eval;


/// <summary>
/// Serializable result of running an <see cref="EvalCaseSet"/>: per-case metrics, the
/// aggregate, per-scope and per-stage breakdowns, threshold violations, and the per-case
/// ranked id lists (with graded gains) for diffing. JSON-ready via System.Text.Json.
/// </summary>
public sealed record EvalReport(
    string SetId,
    int TopK,
    EvalMetrics Aggregate,
    IReadOnlyList<EvalCaseMetrics> Cases,
    IReadOnlyList<EvalMetricBreakdownEntry> PerScope,
    IReadOnlyList<EvalMetricBreakdownEntry> PerStage,
    MetricThresholdViolations ThresholdViolations,
    IReadOnlyList<EvalCaseTrace> Traces,
    long CorpusRecordCount,
    double TotalDurationMs)
{
    public bool AllThresholdsPass => !ThresholdViolations.HasViolations;
}

