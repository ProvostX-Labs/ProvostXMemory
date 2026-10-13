using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

using ProvostXMemory.Eval;

namespace ProvostXMemory;

/// <summary>
/// Runs an <see cref="EvalCaseSet"/> against a freshly built service from an
/// <see cref="EvalServiceFactory"/>: materializes the corpus records under a fresh temp
/// root, builds indexes per scope, replays every golden query, computes all metrics and
/// breakdowns, checks thresholds, and produces a JSON-serializable <see cref="EvalReport"/>.
/// The runner never constructs a service itself — hosts control wiring via the factory.
/// </summary>
public sealed class EvalRunner
{
    private readonly EvalServiceFactory _serviceFactory;
    private readonly EvalMetricMath _metrics;

    public EvalRunner(EvalServiceFactory serviceFactory, EvalMetricMath? metricMath = null)
    {
        ArgumentNullException.ThrowIfNull(serviceFactory);
        _serviceFactory = serviceFactory;
        _metrics = metricMath ?? new EvalMetricMath();
    }

    /// <summary>Loads the corpus into a fresh service rooted at <paramref name="rootPath"/>.</summary>
    public async Task<ProvostXMemory.IMemoryService> LoadAsync(
        DeterministicEvalCorpus corpus,
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var service = _serviceFactory.Create(rootPath);
        foreach (var corpusRecord in corpus.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await service.RememberAsync(corpusRecord.Record, cancellationToken).ConfigureAwait(false);
        }

        foreach (var scope in corpus.Scopes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await service.BuildIndexesAsync(scope, cancellationToken).ConfigureAwait(false);
        }

        return service;
    }

    /// <summary>
    /// Full run on a fresh temp root: load → index → evaluate; the root directory is
    /// deleted afterwards unless <see cref="EvalRunnerOptions.DisposeRootDirectory"/> is false.
    /// </summary>
    public async Task<EvalReport> RunAsync(
        DeterministicEvalCorpus corpus,
        EvalCaseSet caseSet,
        EvalRunnerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(caseSet);
        options ??= new EvalRunnerOptions();

        var rootPath = CreateRootPath();
        try
        {
            var service = await LoadAsync(corpus, rootPath, cancellationToken).ConfigureAwait(false);
            try
            {
                return await EvaluateAsync(service, caseSet, options, corpus.TotalRecords, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
        finally
        {
            if (options.DisposeRootDirectory)
            {
                TryDeleteDirectory(rootPath);
            }
        }
    }

    /// <summary>
    /// Evaluates an already-loaded service: replays each case against its own scope,
    /// truncates to TopK, computes metrics, aggregates and breakdowns, checks thresholds.
    /// <paramref name="corpusRecordCount"/> is informational (0 when unknown).
    /// </summary>
    public async Task<EvalReport> EvaluateAsync(
        ProvostXMemory.IMemoryService service,
        EvalCaseSet caseSet,
        EvalRunnerOptions? options = null,
        long corpusRecordCount = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(caseSet);
        options ??= new EvalRunnerOptions();

        var topK = options.TopK;
        var watch = Stopwatch.StartNew();
        var caseMetrics = new List<EvalCaseMetrics>(caseSet.Cases.Count);
        var traces = new List<EvalCaseTrace>(caseSet.Cases.Count);
        var diagnosticsPerCase = new List<IReadOnlyList<RetrievalStageDiagnostics>>(caseSet.Cases.Count);

        foreach (var evalCase in caseSet.Cases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await service
                .RecallAsync(new RetrievalQuery(evalCase.Query, evalCase.QueryScope, topK), cancellationToken)
                .ConfigureAwait(false);

            // Scope-qualified judging: record ids may repeat across scopes, so a hit is
            // only judgeable against this case when its record's own scope matches the
            // case's scope. Foreign hits are masked out of the ranking (they can never
            // be legitimately relevant here) and reported via ForeignRecordHits.
            var inScopeHits = new List<ScoredMemoryRecord>(result.Records.Count);
            var foreignRecordHits = 0;
            foreach (var hit in result.Records)
            {
                if (MemoryScopeComparer.Ordinal.Equals(hit.Record.Scope, evalCase.QueryScope))
                {
                    inScopeHits.Add(hit);
                }
                else
                {
                    foreignRecordHits++;
                }
            }

            var rankedIds = inScopeHits.Select(hit => hit.Record.Id).ToList();
            var gains = rankedIds.Select(evalCase.RelevanceOf).ToList();
            var relevantRetrieved = gains.Count(gain => gain > 0.0);

            caseMetrics.Add(_metrics.EvaluateCase(evalCase, rankedIds, topK, relevantRetrieved));
            diagnosticsPerCase.Add(result.Diagnostics);
            traces.Add(new EvalCaseTrace(
                CaseId: evalCase.Id,
                ScopeKind: evalCase.QueryScope.Kind.ToString(),
                ScopeId: evalCase.QueryScope.Id,
                ForeignRecordHits: foreignRecordHits,
                Hits: rankedIds
                    .Zip(gains, (id, gain) => new EvalCaseTraceHit(id, gain))
                    .ToList()));
        }

        watch.Stop();

        var aggregate = EvalMetrics.Mean(caseMetrics);
        var perScope = BuildScopeBreakdown(caseMetrics);
        var perStage = BuildStageBreakdown(caseMetrics, diagnosticsPerCase);
        var violations = options.Thresholds.Check(aggregate);

        return new EvalReport(
            SetId: caseSet.Id,
            TopK: topK,
            Aggregate: aggregate,
            Cases: caseMetrics,
            PerScope: perScope,
            PerStage: perStage,
            ThresholdViolations: violations,
            Traces: traces,
            CorpusRecordCount: corpusRecordCount,
            TotalDurationMs: watch.Elapsed.TotalMilliseconds);
    }

    // Breakdown per scope: group case metric rows by (kind, id); intentionally instance method.
    private static IReadOnlyList<EvalMetricBreakdownEntry> BuildScopeBreakdown(
        IReadOnlyList<EvalCaseMetrics> caseMetrics)
    {
        var groups = caseMetrics
            .GroupBy(metrics => (metrics.ScopeKind, metrics.ScopeId))
            .OrderBy(group => group.Key.ScopeKind, StringComparer.Ordinal)
            .ThenBy(group => group.Key.ScopeId, StringComparer.Ordinal)
            .ToList();
        var entries = new List<EvalMetricBreakdownEntry>(groups.Count);
        foreach (var group in groups)
        {
            var rows = group.ToList();
            entries.Add(new EvalMetricBreakdownEntry(
                Key: $"{group.Key.ScopeKind}/{group.Key.ScopeId}",
                Metrics: EvalMetrics.Mean(rows),
                CaseIds: rows.Select(metrics => metrics.CaseId).ToList()));
        }

        return entries;
    }

    /// <summary>
    /// Breakdown per retriever stage, derived from the per-stage diagnostics the service
    /// reports: a case counts toward a stage when that stage ran (non-zero duration) and
    /// produced hits/records. When the service reports no diagnostics at all, every case
    /// is attributed to each requested stage so the breakdown degrades gracefully.
    /// </summary>
    private static IReadOnlyList<EvalMetricBreakdownEntry> BuildStageBreakdown(
        IReadOnlyList<EvalCaseMetrics> caseMetrics,
        IReadOnlyList<IReadOnlyList<RetrievalStageDiagnostics>> diagnosticsPerCase)
    {
        var stageNames = new[] { "dense", "lexical", "fusion" };
        var hasDiagnostics = diagnosticsPerCase.Any(diagnostics => diagnostics.Count > 0);
        var entries = new List<EvalMetricBreakdownEntry>(stageNames.Length);
        foreach (var stage in stageNames)
        {
            var rows = new List<EvalCaseMetrics>();
            var caseIds = new List<string>();
            for (var i = 0; i < caseMetrics.Count; i++)
            {
                var contributed = hasDiagnostics
                    ? StageContributed(diagnosticsPerCase[i], stage)
                    : true; // no diagnostics available — attribute all cases to every stage
                if (contributed)
                {
                    rows.Add(caseMetrics[i]);
                    caseIds.Add(caseMetrics[i].CaseId);
                }
            }

            entries.Add(new EvalMetricBreakdownEntry(
                Key: stage,
                Metrics: EvalMetrics.Mean(rows),
                CaseIds: caseIds));
        }

        return entries;
    }

    // Pure check: stage contributed when its diagnostics ran and produced hits or records.
    private static bool StageContributed(
        IReadOnlyList<RetrievalStageDiagnostics> diagnostics,
        string stage)
    {
        foreach (var entry in diagnostics)
        {
            if (!string.Equals(entry.Stage, stage, StringComparison.Ordinal))
            {
                continue;
            }

            var produced = entry.Scores.TryGetValue("hits", out var hits) && hits > 0
                || entry.Scores.TryGetValue("records", out var records) && records > 0;
            if (entry.Duration > TimeSpan.Zero && produced)
            {
                return true;
            }
        }

        return false;
    }

    // Pure temp-root factory; intentionally static per architecture rule.
    private static string CreateRootPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "provostx-eval",
            Guid.NewGuid().ToString("N"));
    }

    // Best-effort cleanup; intentionally static per architecture rule.
    private static void TryDeleteDirectory(string rootPath)
    {
        try
        {
            Directory.Delete(rootPath, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}