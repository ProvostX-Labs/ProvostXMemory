using System;
using System.Collections.Generic;
using System.Linq;

namespace ProvostXMemory.Eval;


/// <summary>Metrics for one eval case: Recall@k, Precision@k, MRR and NDCG@k over the ranked
/// result ids judged against the case's golden relevance.</summary>
public sealed record EvalCaseMetrics(
    string CaseId,
    string ScopeKind,
    string ScopeId,
    double RecallAtK,
    double PrecisionAtK,
    double Mrr,
    double NdcgAtK,
    int ResultCount,
    int RelevantRetrievedCount,
    int RelevantTotalCount);

/// <summary>Aggregate over all cases: plain means of the per-case metric values.</summary>
public sealed record EvalMetrics(
    double RecallAtK,
    double PrecisionAtK,
    double Mrr,
    double NdcgAtK)
{
    // Stateless utility (pure): aggregate math over the given rows only.
    /// <summary>Mean over all case rows (empty → all-zero metrics).</summary>
    public static EvalMetrics Mean(IReadOnlyList<EvalCaseMetrics> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);
        if (cases.Count == 0)
        {
            return new EvalMetrics(0.0, 0.0, 0.0, 0.0);
        }

        return new EvalMetrics(
            cases.Select(metrics => metrics.RecallAtK).Average(),
            cases.Select(metrics => metrics.PrecisionAtK).Average(),
            cases.Select(metrics => metrics.Mrr).Average(),
            cases.Select(metrics => metrics.NdcgAtK).Average());
    }
}

