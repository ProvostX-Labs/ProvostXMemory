using System;
using System.Collections.Generic;

namespace ProvostXMemory.Eval;


/// <summary>
/// Minimum metric values for the aggregate <see cref="EvalReport"/>. A violation is any
/// actual value strictly below its threshold. All thresholds optional (null = unchecked).
/// </summary>
public sealed class EvalMetricThresholds
{
    public const string MetricRecallAtK = "recall@k";
    public const string MetricPrecisionAtK = "precision@k";
    public const string MetricMrr = "mrr";
    public const string MetricNdcgAtK = "ndcg@k";

    public EvalMetricThresholds(
        double? recallAtK = null,
        double? precisionAtK = null,
        double? mrr = null,
        double? ndcgAtK = null)
    {
        if (recallAtK is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(recallAtK));
        }

        if (precisionAtK is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(precisionAtK));
        }

        if (mrr is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(mrr));
        }

        if (ndcgAtK is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(ndcgAtK));
        }

        RecallAtK = recallAtK;
        PrecisionAtK = precisionAtK;
        Mrr = mrr;
        NdcgAtK = ndcgAtK;
    }

    public double? RecallAtK { get; }

    public double? PrecisionAtK { get; }

    public double? Mrr { get; }

    public double? NdcgAtK { get; }

    /// <summary>Builds the violation list (aggregate level) for this threshold set.</summary>
    public MetricThresholdViolations Check(EvalMetrics aggregate)
    {
        var violations = new List<MetricThresholdViolation>();
        AddIfViolated(violations, MetricRecallAtK, RecallAtK, aggregate.RecallAtK);
        AddIfViolated(violations, MetricPrecisionAtK, PrecisionAtK, aggregate.PrecisionAtK);
        AddIfViolated(violations, MetricMrr, Mrr, aggregate.Mrr);
        AddIfViolated(violations, MetricNdcgAtK, NdcgAtK, aggregate.NdcgAtK);
        return new MetricThresholdViolations(violations);
    }

    // Pure comparison; intentionally static per architecture rule.
    private static void AddIfViolated(
        List<MetricThresholdViolation> violations,
        string metric,
        double? threshold,
        double actual)
    {
        if (threshold is { } required && actual < required)
        {
            violations.Add(new MetricThresholdViolation(metric, actual, required));
        }
    }
}

