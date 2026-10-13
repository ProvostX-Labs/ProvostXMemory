using System;
using System.Collections.Generic;

namespace ProvostXMemory.Eval;


/// <summary>Immutable violation list; empty when all thresholds pass.</summary>
public sealed class MetricThresholdViolations
{
    private static readonly IReadOnlyList<MetricThresholdViolation> EmptyViolations =
        Array.Empty<MetricThresholdViolation>();

    public MetricThresholdViolations(IReadOnlyList<MetricThresholdViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);
        Violations = violations;
    }

    public IReadOnlyList<MetricThresholdViolation> Violations { get; }

    public bool HasViolations => Violations.Count > 0;

    public static MetricThresholdViolations None()
    {
        return new MetricThresholdViolations(EmptyViolations);
    }
}
