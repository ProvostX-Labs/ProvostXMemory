using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Fusion;

namespace ProvostXMemory.Retrieval;

/// <summary>
/// Executes a declarative <see cref="QueryPlan"/>. Each step fans out to its target
/// scopes (target scope strings are "<c>&lt;scope-kind&gt;/&lt;scope-id&gt;</c>" pairs;
/// empty targets mean the query's own scope), runs the injected retriever in every
/// target scope with per-scope ids namespaced, and merges the step lists via the
/// injected <see cref="IResultFuser"/>. When every step's top score is below
/// <see cref="QueryPlanRunnerOptions.WeakEvidenceThreshold"/>, a single second pass
/// runs once against the rewritten text from the injected <see cref="IQueryTextRewriter"/>;
/// its results replace the first pass. Record resolution happens against each target
/// scope's own snapshot — a record id from scope A never resolves into scope B.
/// </summary>
public sealed class QueryPlanRunner : IQueryPlanRunner
{
    private const string RetrieverName = "query-plan";
    private const string SecondPassSuffix = ":pass2";

    private readonly IHybridRetriever _retriever;
    private readonly IScopeIndexRegistry _registry;
    private readonly IResultFuser _fuser;
    private readonly IScopeResolver _scopeResolver;
    private readonly IQueryTextRewriter? _rewriter;
    private readonly QueryPlanRunnerOptions _options;

    /// <summary>Resolves target scope strings ("<c>Kind/Id</c>") against a root scope.</summary>
    public interface IScopeResolver
    {
        /// <summary>Returns the concrete scope for a plan target name; throws when unknown.</summary>
        MemoryScope Resolve(MemoryScope rootScope, string targetName);
    }

    /// <summary>Tuning for the plan runner: weak-evidence thresholds and pass behavior.</summary>
    public sealed class QueryPlanRunnerOptions
    {
        /// <summary>Top score under which a step's evidence counts as weak (triggers the second pass).</summary>
        public double WeakEvidenceThreshold { get; init; } = 0.02;

        /// <summary>Hard cap on fused records retained per step before the next stage.</summary>
        public int MaxPerStep { get; init; } = 50;
    }

    /// <summary>Composes the plan runner; all principal dependencies are injected.</summary>
    public QueryPlanRunner(
        IHybridRetriever retriever,
        IScopeIndexRegistry registry,
        IResultFuser fuser,
        IScopeResolver scopeResolver,
        IQueryTextRewriter? rewriter = null,
        QueryPlanRunnerOptions? options = null)
    {
        _retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _fuser = fuser ?? throw new ArgumentNullException(nameof(fuser));
        _scopeResolver = scopeResolver ?? throw new ArgumentNullException(nameof(scopeResolver));
        _rewriter = rewriter;
        _options = options ?? new QueryPlanRunnerOptions();
    }

    /// <inheritdoc />
    public async Task<RetrievalResult> RunAsync(QueryPlan plan, string text, MemoryScope scope, RetrievalFilters? filters = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<RetrievalStageDiagnostics>();
        var executedPlan = plan;
        var firstPassTopScore = 0.0;

        for (var pass = 1; pass <= 2; pass++)
        {
            var passWatch = System.Diagnostics.Stopwatch.StartNew();
            var (records, topScore) = await ExecuteAsync(executedPlan, text, scope, pass, diagnostics, filters, cancellationToken).ConfigureAwait(false);
            passWatch.Stop();

            diagnostics.Add(new RetrievalStageDiagnostics(
                pass == 1 ? "plan.pass1" : "plan.pass2",
                passWatch.Elapsed,
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["records"] = records.Count,
                    ["top-score"] = topScore,
                }));

            if (pass == 1)
            {
                firstPassTopScore = topScore;
                var weakEvidence = topScore < _options.WeakEvidenceThreshold && records.Count >= 0;
                if (!weakEvidence || _rewriter is null)
                {
                    return new RetrievalResult(records, diagnostics);
                }

                var rewritten = await _rewriter.RewriteAsync(text, cancellationToken).ConfigureAwait(false);
                if (string.Equals(rewritten, text, StringComparison.Ordinal))
                {
                    return new RetrievalResult(records, diagnostics);
                }

                text = rewritten;
                executedPlan = executedPlan with
                {
                    PlanId = $"{plan.PlanId}{SecondPassSuffix}",
                };
                continue;
            }

            // Second pass fully replaces the first; at most one retry runs.
            return new RetrievalResult(records, diagnostics);
        }

        throw new InvalidOperationException("Unreachable: the plan runner executes at most two passes.");
    }

    private async Task<(IReadOnlyList<ScoredMemoryRecord> Records, double TopScore)> ExecuteAsync(
        QueryPlan plan,
        string text,
        MemoryScope rootScope,
        int pass,
        List<RetrievalStageDiagnostics> diagnostics,
        RetrievalFilters? filters,
        CancellationToken cancellationToken)
    {
        var planRecords = new List<ScoredMemoryRecord>();
        var seenRecordIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var step in plan.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (stepRecords, topScore) = await ExecuteStepAsync(plan, step, text, rootScope, pass, diagnostics, filters, cancellationToken).ConfigureAwait(false);
            diagnostics.Add(new RetrievalStageDiagnostics(
                $"plan.step:{step.StepId}",
                TimeSpan.Zero,
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["records"] = stepRecords.Count,
                    ["top-score"] = topScore,
                }));

            foreach (var record in stepRecords)
            {
                if (seenRecordIds.Add(QualifiedId(record.Record)))
                {
                    planRecords.Add(record);
                }
            }
        }

        planRecords = planRecords
            .OrderByDescending(record => record.Score)
            .ThenBy(record => record.Record.Id, StringComparer.Ordinal)
            .ToList();
        var top = planRecords.Count > 0 ? planRecords[0].Score : 0.0;
        return (planRecords, top);
    }

    private async Task<(IReadOnlyList<ScoredMemoryRecord> Records, double TopScore)> ExecuteStepAsync(
        QueryPlan plan,
        QueryPlanStep step,
        string text,
        MemoryScope rootScope,
        int pass,
        List<RetrievalStageDiagnostics> diagnostics,
        RetrievalFilters? filters,
        CancellationToken cancellationToken)
    {
        var targets = ResolveTargets(step, rootScope);
        var stepResults = new List<RetrievalResult>(targets.Count);

        foreach (var targetScope in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetQuery = new RetrievalQuery(
                Text: text,
                Scope: targetScope,
                TopK: step.TopK,
                Filters: filters);
            var result = await _retriever.RetrieveAsync(targetQuery, cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(result);
            stepResults.Add(result);

            diagnostics.Add(new RetrievalStageDiagnostics(
                $"plan.step:{step.StepId}.scope:{targetScope.Kind}/{targetScope.Id}{(pass == 2 ? SecondPassSuffix : string.Empty)}",
                TimeSpan.Zero,
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["records"] = result.Records.Count,
                    ["top-score"] = result.Records.Count > 0 ? result.Records[0].Score : 0.0,
                }));
        }

        // Merge the per-scope result lists with scope-qualified ids so the same record id
        // in two target scopes never conflates (and never merges across scopes).
        var rankedLists = stepResults
            .Select(result => new RankedList(
                $"step:{step.StepId}",
                result.Records
                    .Select(record => new ScoredId(
                        record.Record.Scope.Kind.ToString(),
                        record.Record.Scope.Id,
                        $"{record.Record.Scope.Kind}/{record.Record.Scope.Id}/{record.Record.Id}",
                        record.Score))
                    .ToList()))
            .ToList();

        var fused = _fuser.Fuse(rankedLists.Count > 0 ? rankedLists : EmptyLists(), new FusionOptions());
        var byQualified = new Dictionary<string, ScoredMemoryRecord>(StringComparer.Ordinal);
        foreach (var result in stepResults)
        {
            foreach (var record in result.Records)
            {
                byQualified[QualifiedId(record.Record)] = record;
            }
        }

        var merged = new List<ScoredMemoryRecord>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hit in fused)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!byQualified.TryGetValue(hit.Id, out var record) || !seen.Add(hit.Id))
            {
                continue;
            }

            merged.Add(new ScoredMemoryRecord(
                record.Record,
                hit.Score,
                new RetrievalProvenance(
                    RetrieverName,
                    new Dictionary<string, double>(StringComparer.Ordinal)
                    {
                        [$"plan:{plan.PlanId}.step:{step.StepId}"] = hit.Score,
                    })));
        }

        merged = merged.Take(_options.MaxPerStep).ToList();
        var top = merged.Count > 0 ? merged[0].Score : 0.0;
        return (merged, top);
    }

    private List<MemoryScope> ResolveTargets(QueryPlanStep step, MemoryScope rootScope)
    {
        if (step.TargetScopes.Count == 0)
        {
            return new List<MemoryScope> { rootScope };
        }

        var targets = new List<MemoryScope>(step.TargetScopes.Count);
        foreach (var targetName in step.TargetScopes)
        {
            var resolved = _scopeResolver.Resolve(rootScope, targetName);
            ArgumentNullException.ThrowIfNull(resolved);
            targets.Add(resolved);
        }

        return targets;
    }

    // Pure scope-qualified record id ("<kind>/<id>/<recordId>"); intentional static per architecture rule.
    private static string QualifiedId(MemoryRecord record)
        => $"{record.Scope.Kind}/{record.Scope.Id}/{record.Id}";

    // Pure empty rank-list factory; intentional static per architecture rule.
    private static IReadOnlyList<RankedList> EmptyLists() => Array.Empty<RankedList>();
}