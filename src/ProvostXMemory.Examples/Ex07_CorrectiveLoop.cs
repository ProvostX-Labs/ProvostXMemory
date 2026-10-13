using ProvostXMemory.Abstractions;
using ProvostXMemory.Correction;

namespace ProvostXMemory.Examples;

/// <summary>
/// Corrective recall: a weak first-pass query is graded, rewritten, and retried. The
/// deterministic fake grader accepts only strong fused hits (both retrieval stages
/// contribute); the built-in keyword rewriter distills the noisy query so the retry pass
/// ranks the target record on top. Diagnostics prove the rewrite-and-retry cycle ran.
/// </summary>
public sealed class Ex07_CorrectiveLoop : ExampleBase
{
    /// <summary>
    /// Deterministic fake grader standing in for an LLM judge: a candidate is Relevant
    /// only when it is the top hit of its best fused rank list (RRF score 1/61 ≈ 0.0164
    /// for rank 1, 1/62 ≈ 0.0161 for rank 2) AND its content carries the domain anchor
    /// term "kubernetes". Everything else is Irrelevant. Pure function of the inputs —
    /// same (score, content) always yields the same grade.
    /// </summary>
    private sealed class FusedHitGrader : IRetrievalGrader
    {
        public Task<GradedCandidate> GradeAsync(
            RetrievalQuery query, ScoredMemoryRecord candidate, CancellationToken cancellationToken = default)
        {
            var isTopRankHit = candidate.Score >= 0.0163;
            var carriesAnchor = candidate.Record.Content.Contains("kubernetes", StringComparison.Ordinal);
            var grade = isTopRankHit && carriesAnchor
                ? RetrievalGrade.Relevant
                : RetrievalGrade.Irrelevant;
            return Task.FromResult(new GradedCandidate(candidate, grade));
        }
    }

    public override async Task RunAsync()
    {
        var rig = new ExampleRig();
        var root = rig.FreshRoot("ex07");

        // Wiring a grader switches RecallAsync into the corrective pipeline.
        IMemoryService memory = rig.BuildService(root, grader: new FusedHitGrader());
        var scope = new MemoryScope(MemoryScopeKind.Session, "ops-demo");

        await memory.RememberAsync(new MemoryRecord(
            "target", scope, "rollout-note",
            "kubernetes rollout completed without downtime")).ConfigureAwait(false);
        await memory.RememberAsync(new MemoryRecord(
            "noise-1", scope, "standup-note",
            "standup notes about sprint planning and the offsite")).ConfigureAwait(false);
        await memory.BuildIndexesAsync(scope).ConfigureAwait(false);

        // A noisy, stopword-heavy query: the first pass ranks noise and the weakly-ranked
        // target fails the grade, so the loop rewrites (stopwords distilled) and retries;
        // the retry ranks the target first and the grader accepts it.
        var weakQuery = "the the the rollout thing with all the extra filler words";
        var result = await memory.RecallAsync(new RetrievalQuery(weakQuery, scope, TopK: 3))
            .ConfigureAwait(false);

        Console.WriteLine($"Weak query: \"{weakQuery}\"");
        Console.WriteLine("Diagnostics (stage → stages run):");
        foreach (var diagnostic in result.Diagnostics)
        {
            Console.WriteLine($"  {diagnostic.Stage,-24} {diagnostic.Duration.TotalMilliseconds,7:0.000} ms");
        }

        Console.WriteLine("Accepted hits after grading:");
        if (result.Records.Count == 0)
        {
            Console.WriteLine("  (none)");
        }

        foreach (var hit in result.Records)
        {
            Console.WriteLine($"  {hit.Score:0.0000}  {hit.Record.Id}: {hit.Record.Content}");
        }

        Console.WriteLine();
        Console.WriteLine("The 'correction.rewritten' diagnostic proves the first pass failed the grade");
        Console.WriteLine("and the distilled query was retried before anything was accepted.");
    }
}
