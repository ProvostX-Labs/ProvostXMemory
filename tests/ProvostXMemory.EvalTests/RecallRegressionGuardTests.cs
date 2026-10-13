using System;
using System.IO;
using System.Threading.Tasks;

using ProvostXMemory.Eval;

using Xunit;

namespace ProvostXMemory.EvalTests;

/// <summary>
/// THE recall@k regression fixture the architecture calls for: the default hybrid pipeline
/// must hold Recall@10 ≥ 0.9 on the deterministic corpus. Built entirely on the seeded
/// fake embedder (no real model) — deterministic across machines and runs. If this test
/// fails, a retrieval regression landed.
/// </summary>
public sealed class RecallRegressionGuardTests
{
    private const double MinimumRecallAt10 = 0.9;
    private const int CorpusSeed = 2026;

    [Fact]
    public async Task Recall_at_10_stays_at_or_above_0_9_on_deterministic_corpus()
    {
        var corpus = new DeterministicEvalCorpus(
            seed: CorpusSeed,
            scopeCount: 3,
            recordCountPerScope: 24);
        var caseSet = corpus.BuildEvalCaseSet();
        var runner = new EvalRunner(new DefaultEvalServiceFactory(vectorDim: 128));
        var options = new EvalRunnerOptions(
            topK: 10,
            thresholds: new EvalMetricThresholds(recallAtK: MinimumRecallAt10));

        var report = await runner.RunAsync(corpus, caseSet, options);

        Assert.False(
            report.ThresholdViolations.HasViolations,
            $"Recall@10 regression: aggregate {report.Aggregate.RecallAtK} < {MinimumRecallAt10}. " +
            $"Violations: {string.Join(", ", report.ThresholdViolations.Violations)}");
        Assert.True(
            report.Aggregate.RecallAtK >= MinimumRecallAt10,
            $"Recall@10 regression guard tripped: {report.Aggregate.RecallAtK}");
    }

    [Fact]
    public async Task Regression_guard_is_deterministic_across_repeated_runs()
    {
        var corpus = new DeterministicEvalCorpus(
            seed: CorpusSeed,
            scopeCount: 2,
            recordCountPerScope: 16);
        var caseSet = corpus.BuildEvalCaseSet();
        var runner = new EvalRunner(new DefaultEvalServiceFactory(vectorDim: 128));
        var options = new EvalRunnerOptions(topK: 10);

        var first = await runner.RunAsync(corpus, caseSet, options);
        var second = await runner.RunAsync(corpus, caseSet, options);

        Assert.Equal(first.Aggregate.RecallAtK, second.Aggregate.RecallAtK, precision: 12);
        Assert.Equal(first.Aggregate.NdcgAtK, second.Aggregate.NdcgAtK, precision: 12);
        Assert.Equal(first.Aggregate.Mrr, second.Aggregate.Mrr, precision: 12);
    }
}