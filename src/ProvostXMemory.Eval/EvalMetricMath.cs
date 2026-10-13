using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Eval;

/// <summary>
/// The metric formulas, implemented independently of any search engine and unit-tested
/// against hand-computed values:
/// <list type="bullet">
/// <item>Recall@k = |relevant ∩ top-k| / |relevant| (0 when no relevant ids).</item>
/// <item>Precision@k = |relevant ∩ top-k| / k (unfilled top-k counts against precision).</item>
/// <item>MRR = 1 / rank of the first relevant hit, else 0.</item>
/// <item>NDCG@k = DCG@k / IDCG@k with DCG = Σ relᵢ / log₂(rank + 1), rank starting at 1.</item>
/// </list>
/// All instance methods (no static state shared across instances).
/// </summary>
public sealed class EvalMetricMath
{
    /// <summary>Recall@k over ordered result ids.</summary>
    public double RecallAtK(IReadOnlyList<string> rankedIds, IReadOnlyList<string> relevantIds, int k)
    {
        ArgumentNullException.ThrowIfNull(rankedIds);
        ArgumentNullException.ThrowIfNull(relevantIds);
        ArgumentOutOfRangeException.ThrowIfLessThan(k, 1);
        if (relevantIds.Count == 0)
        {
            return 0.0;
        }

        var relevant = new HashSet<string>(relevantIds, StringComparer.Ordinal);
        var hit = 0;
        var limit = Math.Min(k, rankedIds.Count);
        for (var i = 0; i < limit; i++)
        {
            if (relevant.Contains(rankedIds[i]))
            {
                hit++;
            }
        }

        return (double)hit / relevantIds.Count;
    }

    /// <summary>Precision@k over ordered result ids; k counts in the denominator, not hits.</summary>
    public double PrecisionAtK(IReadOnlyList<string> rankedIds, IReadOnlyList<string> relevantIds, int k)
    {
        ArgumentNullException.ThrowIfNull(rankedIds);
        ArgumentNullException.ThrowIfNull(relevantIds);
        ArgumentOutOfRangeException.ThrowIfLessThan(k, 1);
        if (relevantIds.Count == 0)
        {
            return 0.0;
        }

        var relevant = new HashSet<string>(relevantIds, StringComparer.Ordinal);
        var hit = 0;
        var limit = Math.Min(k, rankedIds.Count);
        for (var i = 0; i < limit; i++)
        {
            if (relevant.Contains(rankedIds[i]))
            {
                hit++;
            }
        }

        return (double)hit / k;
    }

    /// <summary>Reciprocal rank of the first relevant hit (0 when none in top-k).</summary>
    public double Mrr(IReadOnlyList<string> rankedIds, IReadOnlyList<string> relevantIds, int k)
    {
        ArgumentNullException.ThrowIfNull(rankedIds);
        ArgumentNullException.ThrowIfNull(relevantIds);
        ArgumentOutOfRangeException.ThrowIfLessThan(k, 1);
        var relevant = new HashSet<string>(relevantIds, StringComparer.Ordinal);
        var limit = Math.Min(k, rankedIds.Count);
        for (var i = 0; i < limit; i++)
        {
            if (relevant.Contains(rankedIds[i]))
            {
                return 1.0 / (i + 1);
            }
        }

        return 0.0;
    }

    /// <summary>NDCG@k using graded relevance (binary 1 when the id is in <paramref name="relevantIds"/>).</summary>
    public double NdcgAtK(IReadOnlyList<string> rankedIds, IReadOnlyList<string> relevantIds, int k)
    {
        ArgumentNullException.ThrowIfNull(rankedIds);
        ArgumentNullException.ThrowIfNull(relevantIds);
        ArgumentOutOfRangeException.ThrowIfLessThan(k, 1);
        if (relevantIds.Count == 0)
        {
            return 0.0;
        }

        var relevant = new HashSet<string>(relevantIds, StringComparer.Ordinal);
        var dcg = 0.0;
        var limit = Math.Min(k, rankedIds.Count);
        for (var i = 0; i < limit; i++)
        {
            if (relevant.Contains(rankedIds[i]))
            {
                dcg += 1.0 / Math.Log2(i + 2); // rank i+1 → log2(i+2)
            }
        }

        var idealCount = Math.Min(relevantIds.Count, k);
        var idcg = 0.0;
        for (var rank = 1; rank <= idealCount; rank++)
        {
            idcg += 1.0 / Math.Log2(rank + 1);
        }

        return idcg <= 0.0 ? 0.0 : dcg / idcg;
    }

    /// <summary>NDCG@k with an explicit gain per judged id (graded relevance, gains in [0,1]).</summary>
    public double NdcgAtKGraded(IReadOnlyList<string> rankedIds, IReadOnlyList<double> gradedGains, int k)
    {
        ArgumentNullException.ThrowIfNull(rankedIds);
        ArgumentNullException.ThrowIfNull(gradedGains);
        ArgumentOutOfRangeException.ThrowIfLessThan(k, 1);
        var dcg = 0.0;
        var limit = Math.Min(k, rankedIds.Count);
        for (var i = 0; i < limit; i++)
        {
            var gain = i < gradedGains.Count ? gradedGains[i] : 0.0;
            if (gain > 0.0)
            {
                dcg += gain / Math.Log2(i + 2);
            }
        }

        var ideal = gradedGains
            .Where(gain => gain > 0.0)
            .OrderByDescending(gain => gain)
            .Take(k)
            .ToList();
        var idcg = 0.0;
        for (var rank = 1; rank <= ideal.Count; rank++)
        {
            idcg += ideal[rank - 1] / Math.Log2(rank + 1);
        }

        return idcg <= 0.0 ? 0.0 : dcg / idcg;
    }

    /// <summary>Full metric set for one case over ordered result ids.</summary>
    public EvalCaseMetrics EvaluateCase(
        EvalCase evalCase,
        IReadOnlyList<string> rankedIds,
        int topK,
        int relevantRetrievedCount)
    {
        ArgumentNullException.ThrowIfNull(evalCase);
        ArgumentNullException.ThrowIfNull(rankedIds);
        return new EvalCaseMetrics(
            CaseId: evalCase.Id,
            ScopeKind: evalCase.QueryScope.Kind.ToString(),
            ScopeId: evalCase.QueryScope.Id,
            RecallAtK: evalCase.RelevantIds.Count == 0
                ? 0.0
                : RecallAtK(rankedIds, evalCase.RelevantIds, topK),
            PrecisionAtK: evalCase.RelevantIds.Count == 0
                ? 0.0
                : PrecisionAtK(rankedIds, evalCase.RelevantIds, topK),
            Mrr: Mrr(rankedIds, evalCase.RelevantIds, topK),
            NdcgAtK: NdcgAtK(rankedIds, evalCase.RelevantIds, topK),
            ResultCount: rankedIds.Count,
            RelevantRetrievedCount: relevantRetrievedCount,
            RelevantTotalCount: evalCase.RelevantIds.Count);
    }
}