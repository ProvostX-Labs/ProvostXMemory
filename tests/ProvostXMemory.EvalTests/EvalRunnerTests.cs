using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Eval;

using Xunit;

namespace ProvostXMemory.EvalTests;

/// <summary>
/// EvalRunner end-to-end on the real default pipeline: load → index → evaluate,
/// JSON round-trip, thresholds, isolation (no foreign records), temp-root cleanup.
/// </summary>
public sealed class EvalRunnerTests : IDisposable
{
    private static readonly int ScopeCount = 2;
    private static readonly int RecordsPerScope = 16;

    private readonly string _rootDir =
        Path.Combine(Path.GetTempPath(), "provostx-eval-runner-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_rootDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static DeterministicEvalCorpus Corpus()
    => new(seed: 2026, ScopeCount, RecordsPerScope);

    [Fact]
    public async Task Load_then_evaluate_produces_perfect_metrics_on_deterministic_corpus()
    {
        var corpus = Corpus();
        var caseSet = corpus.BuildEvalCaseSet();
        var rootPath = Path.Combine(_rootDir, Guid.NewGuid().ToString("N"));
        var runner = new EvalRunner(new DefaultEvalServiceFactory());

        var service = await runner.LoadAsync(corpus, rootPath);
        try
        {
            var report = await runner.EvaluateAsync(service, caseSet, new EvalRunnerOptions(topK: 10), corpus.TotalRecords);

            Assert.Equal(caseSet.Id, report.SetId);
            Assert.Equal(10, report.TopK);
            Assert.Equal(corpus.TotalRecords, report.CorpusRecordCount);
            Assert.NotEmpty(report.Cases);
            Assert.Equal(caseSet.Cases.Count, report.Cases.Count);

            // With the cluster-preserving fake embedder the default hybrid pipeline must
            // retrieve essentially everything: recall ≥ 0.9, rank quality near-perfect.
            // Precision@10 caps at 2 relevant / 10 slots = 0.2 on this corpus (recordsPerScope
            // = 16 → 2 records per cluster), which is exactly what a perfect retriever yields.
            Assert.True(report.Aggregate.RecallAtK >= 0.9, $"recall@10 {report.Aggregate.RecallAtK}");
            Assert.True(report.Aggregate.PrecisionAtK >= 0.19, $"precision@10 {report.Aggregate.PrecisionAtK}");
            Assert.True(report.Aggregate.Mrr >= 0.9, $"mrr {report.Aggregate.Mrr}");
            Assert.True(report.Aggregate.NdcgAtK >= 0.9, $"ndcg@10 {report.Aggregate.NdcgAtK}");
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_cleans_up_its_temp_root()
    {
        var corpus = Corpus();
        var caseSet = corpus.BuildEvalCaseSet();
        var runner = new EvalRunner(new DefaultEvalServiceFactory());

        var report = await runner.RunAsync(corpus, caseSet, new EvalRunnerOptions(topK: 10));

        Assert.NotEmpty(report.Cases);
        // The private root is <temp>/provostx-eval/<guid>; no stray dirs may remain.
        var evalRoot = Path.Combine(Path.GetTempPath(), "provostx-eval");
        var leftovers = Directory.Exists(evalRoot) ? Directory.GetFiles(evalRoot, "*", SearchOption.AllDirectories) : Array.Empty<string>();
        Assert.Empty(leftovers);
    }

    [Fact]
    public async Task Report_round_trips_through_json()
    {
        var corpus = Corpus();
        var caseSet = corpus.BuildEvalCaseSet();
        var runner = new EvalRunner(new DefaultEvalServiceFactory());
        var report = await runner.RunAsync(corpus, caseSet, new EvalRunnerOptions(topK: 5));

        var json = new EvalReportJson().Write(report);
        var restored = new EvalReportJson().Read(json);

        Assert.NotNull(restored);
        Assert.Equal(report.SetId, restored.SetId);
        Assert.Equal(report.TopK, restored.TopK);
        Assert.Equal(report.CorpusRecordCount, restored.CorpusRecordCount);
        Assert.Equal(report.Aggregate.RecallAtK, restored.Aggregate.RecallAtK, precision: 9);
        Assert.Equal(report.Aggregate.Mrr, restored.Aggregate.Mrr, precision: 9);
        Assert.Equal(report.Aggregate.NdcgAtK, restored.Aggregate.NdcgAtK, precision: 9);
        Assert.Equal(report.Cases.Count, restored.Cases.Count);
        Assert.Equal(report.Traces.Count, restored.Traces.Count);
        foreach (var pair in report.Cases.Zip(restored.Cases, (first, second) => (first, second)))
        {
            Assert.Equal(pair.first.CaseId, pair.second.CaseId);
            Assert.Equal(pair.first.RecallAtK, pair.second.RecallAtK, precision: 9);
        }

        // traces round-trip with hit ids + gains
        var firstTrace = report.Traces[0];
        var restoredTrace = restored.Traces[0];
        Assert.Equal(firstTrace.CaseId, restoredTrace.CaseId);
        Assert.Equal(firstTrace.Hits.Count, restoredTrace.Hits.Count);
        if (firstTrace.Hits.Count > 0)
        {
            Assert.Equal(firstTrace.Hits[0].RecordId, restoredTrace.Hits[0].RecordId);
            Assert.Equal(firstTrace.Hits[0].Gain, restoredTrace.Hits[0].Gain, precision: 9);
        }
    }

    [Fact]
    public async Task Threshold_violation_marks_report_failed()
    {
        var corpus = Corpus();
        // Degraded-corpus variant: relevance is deliberately misaligned by one cluster
        // (query terms of cluster c, gold set of cluster c+1). Retrieval itself works well
        // on this corpus now that dense fusion works, so the violation is a genuine quality
        // failure judged against the shifted gold set — not a structural cap. (Before the
        // dense-resolve fix the lexical-only pipeline made precision@5 structurally cap at
        // 0.4; that cap argument no longer holds, so the degradation is made explicit.)
        var baseSet = corpus.BuildEvalCaseSet();
        var degradedCases = baseSet.Cases
            .Select(evalCase => new EvalCase(
                evalCase.Id,
                evalCase.Query,
                evalCase.QueryScope,
                corpus.RecordIdsOf(
                    evalCase.QueryScope,
                    (int.Parse(evalCase.Id[^2..], CultureInfo.InvariantCulture) + 1) % corpus.ClusterCountUsed)))
            .ToList();
        var caseSet = new EvalCaseSet(baseSet.Id, degradedCases);
        var runner = new EvalRunner(new DefaultEvalServiceFactory());
        var options = new EvalRunnerOptions(
            topK: 5,
            thresholds: new EvalMetricThresholds(precisionAtK: 0.5));

        var report = await runner.RunAsync(corpus, caseSet, options);

        // Each case has only 2 relevant records → precision@5 caps at 0.4 even for a
        // perfect retriever, so the 0.5 threshold is a guaranteed structural violation.
        Assert.True(report.ThresholdViolations.HasViolations);
        var violation = Assert.Single(report.ThresholdViolations.Violations);
        Assert.Equal(EvalMetricThresholds.MetricPrecisionAtK, violation.Metric);
        Assert.True(violation.Actual < violation.Required, $"actual {violation.Actual}");
        Assert.Equal(0.5, violation.Required, precision: 9);
        Assert.False(report.AllThresholdsPass);
    }

    [Fact]
    public async Task Threshold_pass_yields_clean_report()
    {
        var corpus = Corpus();
        var caseSet = corpus.BuildEvalCaseSet();
        var runner = new EvalRunner(new DefaultEvalServiceFactory());
        var options = new EvalRunnerOptions(topK: 10, thresholds: new EvalMetricThresholds(recallAtK: 0.9));

        var report = await runner.RunAsync(corpus, caseSet, options);

        Assert.False(report.ThresholdViolations.HasViolations);
        Assert.True(report.AllThresholdsPass);
    }

    [Fact]
    public async Task Scope_isolation_no_foreign_records_in_any_case()
    {
        var corpus = Corpus();
        var caseSet = corpus.BuildEvalCaseSet();
        var runner = new EvalRunner(new DefaultEvalServiceFactory());
        var report = await runner.RunAsync(corpus, caseSet, new EvalRunnerOptions(topK: 10));

        Assert.NotEmpty(report.Traces);
        Assert.All(report.Traces, trace => Assert.Equal(0, trace.ForeignRecordHits));
        // and per-scope breakdown has exactly one entry per corpus scope
        Assert.Equal(ScopeCount, report.PerScope.Count);
    }

    [Fact]
    public async Task Same_record_id_in_two_scopes_stays_scoped_and_metrics_stay_correct()
    {
        // The wave-4c robustness property, previously uncovered because the deterministic
        // corpus generates globally unique ids: the same record id ("pg-b") exists in BOTH
        // scopes, but the west copy holds kubernetes content — a deliberate trap. Scope-qualified
        // judging must resolve east query hits to the east record (gain 1, zero foreign hits),
        // never to the west copy (foreign hit, gain confusion).
        var factory = new DefaultEvalServiceFactory();
        var rootPath = Path.Combine(_rootDir, Guid.NewGuid().ToString("N"));
        var service = factory.Create(rootPath);
        var west = new MemoryScope(MemoryScopeKind.Tenant, "eval-west");
        var east = new MemoryScope(MemoryScopeKind.Tenant, "eval-east");
        await service.RememberAsync(new MemoryRecord("k8s-a", west, "k8s-a", "kubernetes cluster rollout helm chart release"));
        await service.RememberAsync(new MemoryRecord("pg-b", west, "pg-b-w", "kubernetes cluster rollout helm chart release"));
        await service.RememberAsync(new MemoryRecord("pg-b", east, "pg-b-e", "postgres migration vacuum tablespace lock index"));
        await service.BuildIndexesAsync(west);
        await service.BuildIndexesAsync(east);
        try
        {
            var caseSet = new EvalCaseSet("repeated-ids-across-scopes", new[]
            {
                new EvalCase("west-k8s", "kubernetes cluster rollout helm", west, new[] { "k8s-a" }),
                new EvalCase("east-pg", "postgres migration vacuum tablespace", east, new[] { "pg-b" }),
            });
            var runner = new EvalRunner(factory);
            var report = await runner.EvaluateAsync(
                service, caseSet, new EvalRunnerOptions(topK: 5, thresholds: new EvalMetricThresholds(recallAtK: 0.9)), 3);

            // Every hit is judged against its own scope: the west "pg-b" (kubernetes content)
            // never leaks into the east case (foreign hit) and vice versa.
            Assert.All(report.Traces, trace => Assert.Equal(0, trace.ForeignRecordHits));
            Assert.True(report.AllThresholdsPass, $"violations: {string.Join(", ", report.ThresholdViolations.Violations)}");

            var westTrace = Assert.Single(report.Traces, trace => trace.CaseId == "west-k8s");
            Assert.Contains(westTrace.Hits, hit => hit.RecordId == "k8s-a" && hit.Gain > 0.0);
            var eastTrace = Assert.Single(report.Traces, trace => trace.CaseId == "east-pg");
            // The pg-b hit must be the east record (postgres content) — the west copy would
            // surface the kubernetes query terms instead and count as a foreign hit above.
            Assert.Contains(eastTrace.Hits, hit => hit.RecordId == "pg-b" && hit.Gain > 0.0);
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    [Fact]
    public async Task Per_stage_breakdown_present_when_diagnostics_available()
    {
        var corpus = Corpus();
        var caseSet = corpus.BuildEvalCaseSet();
        var runner = new EvalRunner(new DefaultEvalServiceFactory());
        var report = await runner.RunAsync(corpus, caseSet, new EvalRunnerOptions(topK: 10));

        Assert.NotEmpty(report.PerStage);
        Assert.Contains(report.PerStage, entry => entry.Key == "dense");
        Assert.Contains(report.PerStage, entry => entry.Key == "lexical");
        // at least one stage contributed for at least one case
        Assert.Contains(report.PerStage, entry => entry.CaseIds.Count > 0);
    }

    [Fact]
    public void Runner_rejects_null_arguments()
    {
        var runner = new EvalRunner(new DefaultEvalServiceFactory());
        var corpus = Corpus();
        var caseSet = corpus.BuildEvalCaseSet();

        Assert.Throws<ArgumentNullException>(() => new EvalRunner(null!));
        Assert.Throws<ArgumentNullException>(() => runner.RunAsync(null!, caseSet).GetAwaiter().GetResult());
        Assert.Throws<ArgumentNullException>(() => runner.RunAsync(corpus, null!).GetAwaiter().GetResult());
    }

    [Fact]
    public async Task Cancellation_token_honored_during_load()
    {
        var corpus = Corpus();
        var runner = new EvalRunner(new DefaultEvalServiceFactory());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runner.LoadAsync(corpus, Path.Combine(_rootDir, "cancel"), cts.Token));
    }
}
