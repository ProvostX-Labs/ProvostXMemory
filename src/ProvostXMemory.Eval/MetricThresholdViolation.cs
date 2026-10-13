using System;
using System.Collections.Generic;

namespace ProvostXMemory.Eval;


/// <summary>One threshold violation: metric name, actual vs required.</summary>
public sealed record MetricThresholdViolation(
    string Metric,
    double Actual,
    double Required);

