using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Retrieval;

namespace ProvostXMemory.Correction;

/// <summary>
/// Grader-gated corrective retrieval around an injected <see cref="IHybridRetriever"/>:
/// candidates pass or are dropped per <see cref="IRetrievalGrader"/>; below-floor
/// candidates are not yielded. One rewrite-and-retry cycle via <see cref="IQueryRewriter"/>
/// runs when the first pass graded any candidate <see cref="RetrievalGrade.Uncertain"/> or
/// yielded nothing acceptable (all-Irrelevant/all-below-floor). When nothing survives, the
/// optional <see cref="IExternalFetcher"/> hook (null = skip) runs once. Every stage
/// reports diagnostics.
/// </summary>
public sealed class CorrectiveRetriever : ICorrectiveRetriever
{
    private const string RetrieverName = "corrective";

    /// <summary>Diagnostic stage names reported in <see cref="RetrievalStageDiagnostics.Stage"/>.</summary>
    public static class StageNames
    {
        /// <summary>Fan-in of the injected hybrid retriever.</summary>
        public const string Hybrid = "correction.hybrid";

        /// <summary>Grading of all candidates on the first pass.</summary>
        public const string Graded = "correction.graded";

        /// <summary>Query rewrite feeding the retry pass.</summary>
        public const string Rewritten = "correction.rewritten";

        /// <summary>Grading of the retry candidates (second pass).</summary>
        public const string GradedRetry = "correction.graded-retry";

        /// <summary>External fetch stage (absent when skipped, fetcher null, or disabled).</summary>
        public const string Fetched = "correction.fetched";
    }

    private readonly IHybridRetriever _hybridRetriever;
    private readonly IRetrievalGrader _grader;
    private readonly IQueryRewriter _rewriter;
    private readonly IExternalFetcher? _fetcher;
    private readonly CorrectionOptions _options;

    /// <summary>Composes the corrective pipeline; all principal dependencies are injected.</summary>
    public CorrectiveRetriever(
        IHybridRetriever hybridRetriever,
        IRetrievalGrader grader,
        IQueryRewriter rewriter,
        IExternalFetcher? fetcher = null,
        CorrectionOptions? options = null)
    {
        _hybridRetriever = hybridRetriever ?? throw new ArgumentNullException(nameof(hybridRetriever));
        _grader = grader ?? throw new ArgumentNullException(nameof(grader));
        _rewriter = rewriter ?? throw new ArgumentNullException(nameof(rewriter));
        _fetcher = fetcher; // Null is legal: the external fetch hook is optional (skipped).
        _options = options ?? new CorrectionOptions();
    }

    /// <inheritdoc />
    public async Task<RetrievalResult> RetrieveAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<RetrievalStageDiagnostics>();

        // ---- Stage 1: hybrid fan-in ---------------------------------------------------
        var hybridWatch = Stopwatch.StartNew();
        var hybridResult = await _hybridRetriever.RetrieveAsync(query, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(hybridResult);
        hybridWatch.Stop();
        diagnostics.Add(new RetrievalStageDiagnostics(
            StageNames.Hybrid,
            hybridWatch.Elapsed,
            new Dictionary<string, double>(StringComparer.Ordinal)));

        // ---- Stage 2: grade every candidate on the first pass -------------------------
        var gradedWatch = Stopwatch.StartNew();
        var graded = await GradeAllAsync(query, hybridResult.Records, cancellationToken).ConfigureAwait(false);

        var firstPassAccepted = SelectAccepted(graded);

        // ---- Stage 3+4: one rewrite-and-retry cycle -----------------------------------
        // Triggered by any Uncertain grade or by an empty first pass (all relevant
        // candidates dropped or missing); retry outcomes never re-trigger.
        if (graded.Exists(item => item.Grade == RetrievalGrade.Uncertain) || firstPassAccepted.Count == 0)
        {
            var rewriteWatch = Stopwatch.StartNew();
            var rewrittenText = await _rewriter.RewriteAsync(query, cancellationToken).ConfigureAwait(false);
            rewriteWatch.Stop();
            diagnostics.Add(new RetrievalStageDiagnostics(
                StageNames.Rewritten,
                rewriteWatch.Elapsed,
                new Dictionary<string, double>(StringComparer.Ordinal)));

            var retryQuery = query with { Text = rewrittenText };
            var retryResult = await _hybridRetriever.RetrieveAsync(retryQuery, cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(retryResult);
            var gradedRetry = await GradeAllAsync(query, retryResult.Records, cancellationToken).ConfigureAwait(false);

            // The retry pass fully replaces the first pass; at most one retry runs.
            graded = gradedRetry;
        }
        else
        {
            gradedWatch.Stop();
            diagnostics.Add(new RetrievalStageDiagnostics(
                StageNames.Graded,
                gradedWatch.Elapsed,
                new Dictionary<string, double>(StringComparer.Ordinal)));

            return new RetrievalResult(firstPassAccepted, diagnostics);
        }

        gradedWatch.Stop();
        diagnostics.Add(new RetrievalStageDiagnostics(
            StageNames.GradedRetry,
            gradedWatch.Elapsed,
            new Dictionary<string, double>(StringComparer.Ordinal)));

        // ---- Stage 5: gate by grade and configured floor -------------------------------
        var accepted = SelectAccepted(graded);

        // ---- Stage 6: external fetch hook when nothing survived (null fetcher = skip) --
        if (accepted.Count == 0 && _fetcher is not null && _options.UseFetcherOnEmpty)
        {
            var fetchWatch = Stopwatch.StartNew();
            IReadOnlyList<string>? snippets = await _fetcher.FetchAsync(query, cancellationToken).ConfigureAwait(false);
            fetchWatch.Stop();
            snippets ??= Array.Empty<string>();
            var scores = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var snippet in snippets)
            {
                ArgumentNullException.ThrowIfNull(snippet);
                scores[$"{RetrieverName}.snippet:{scores.Count}"] = 1.0;
            }

            diagnostics.Add(new RetrievalStageDiagnostics(StageNames.Fetched, fetchWatch.Elapsed, scores));
        }

        return new RetrievalResult(accepted, diagnostics);
    }

    private IReadOnlyList<ScoredMemoryRecord> SelectAccepted(IEnumerable<GradedCandidate> graded)
    {
        return graded
            .Where(item => item.Grade == RetrievalGrade.Relevant)
            .Where(item => item.Candidate.Score >= _options.RelevanceFloor)
            .Select(item => item.Candidate)
            .ToList();
    }

    private async Task<List<GradedCandidate>> GradeAllAsync(
        RetrievalQuery originalQuery,
        IEnumerable<ScoredMemoryRecord> candidates,
        CancellationToken cancellationToken)
    {
        var graded = new List<GradedCandidate>();
        foreach (var candidate in candidates)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            var gradedCandidate = await _grader.GradeAsync(originalQuery, candidate, cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(gradedCandidate);
            graded.Add(gradedCandidate);
        }

        return graded;
    }
}