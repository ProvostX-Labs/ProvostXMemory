# Evaluation

The `ProvostXMemory.Eval` package measures retrieval quality with standard ranking metrics, guards against regressions, and produces JSON reports. It is the same harness used by the library's own journey tests (golden-query recall gate at Recall@5 ≥ 0.9, achieved 1.0).

## Usage

```csharp
var factory = new DefaultEvalServiceFactory(vectorDim: 768);
var runner = new EvalRunner(factory);

var caseSet = new EvalCaseSet("release-notes", new[]
{
    new EvalCase(
        CaseId: "q-01",
        Query: "kubernetes rollout",
        Scope: scope,
        RelevantIds: new[] { "note-1" },
        RelevantKeys: Array.Empty<string>())  // optional key hints
    // ...
});

EvalRunnerOptions options = new(
    TopK: 10,
    Thresholds: new EvalMetricThresholds(recallAtK: 0.9));

var report = await runner.EvaluateAsync(caseSet, options);
```

The `EvalServiceFactory` abstraction builds a self-contained service (store, embedder, indexes) per evaluation; `DefaultEvalServiceFactory` composes the standard stack, and `DeterministicEvalCorpus` generates reproducible synthetic corpora (`ClusterHashEmbedder` provides deterministic embeddings with a fixed noise amplitude). `disposeRootDirectory` controls cleanup of the temporary root.

## Metrics

Let *R* be the set of relevant record ids for a case, *k* the cut-off, and *d(i)* the rank position of the *i*-th relevant result in the returned ranking.

| Metric | Formula | Meaning |
|---|---|---|
| Recall@k | &#124;R ∩ top-k&#124; / &#124;R&#124; | Fraction of relevant records found within the top *k*. |
| Precision@k | &#124;R ∩ top-k&#124; / k | Fraction of the top *k* that is relevant. |
| MRR | (1/&#124;R&#124;) · Σ 1/d(i) | Mean of reciprocal ranks of the first relevant hit (1.0 when the first result is relevant). |
| NDCG@k | DCG@k / IDCG@k, where DCG@k = Σ<sub>i≤k</sub> gain(i)/log<sub>2</sub>(i+1) | Discounted cumulative gain normalized by the ideal ranking; supports graded gains from per-hit traces. |

Metric math lives in `EvalMetricMath` (hand-verified against known answers, pinned by `EvalMetricMathTests`). Per-case results are `EvalCaseMetrics`; per-case hit traces (`EvalCaseTrace`, `EvalCaseTraceHit`) carry ids, scores, and gains.

## Reports

`EvalReportJson` serializes a full report — aggregate metrics plus per-scope and per-stage breakdowns, threshold evaluations, and traces — in snake_case JSON. Round-trips are byte-identical (canonical rewrite), so reports diff cleanly in version control.

## Regression guarding

`EvalMetricThresholds` declares per-metric floors:

```csharp
new EvalMetricThresholds(recallAtK: 0.90, mrr: 0.8);
```

Violations are reported as `MetricThresholdViolation` entries (metric, actual, threshold) collected in `MetricThresholdViolations`. A non-empty violation list means the harness fails the run — wire it into CI as the recall-regression guard: fix the corpus and golden queries once, then any retrieval change that drops quality below the floors fails the build instead of shipping.

Recommended practice:

1. Keep golden queries per scope in version control (the `EvalCaseSet` is plain data).
2. Pin thresholds slightly below observed baselines so noise does not flap.
3. Diff serialized reports after intentional changes to see exactly which cases regressed.
