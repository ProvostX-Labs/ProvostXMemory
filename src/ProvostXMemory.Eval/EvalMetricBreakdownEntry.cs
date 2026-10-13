using System;
using System.Collections.Generic;
using System.Linq;

namespace ProvostXMemory.Eval;


/// <summary>Metric means for a breakdown group (scope or retriever stage).</summary>
public sealed record EvalMetricBreakdownEntry(
    string Key,
    EvalMetrics Metrics,
    IReadOnlyList<string> CaseIds);
