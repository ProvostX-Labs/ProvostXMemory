using ProvostXMemory.Abstractions;
using ProvostXMemory.Eval;

namespace ProvostXMemory.Examples;

/// <summary>
/// Eval harness: the deterministic seeded corpus is loaded into a fresh service, every
/// golden query is replayed, and a Recall@k / MRR / NDCG report is printed. Identical
/// seeds produce byte-identical corpora and runs.
/// </summary>
public sealed class Ex09_EvalHarness : ExampleBase
{
    public override async Task RunAsync()
    {
        // Two scopes × 6 records across 2 keyword clusters — small and fully deterministic.
        var corpus = new DeterministicEvalCorpus(seed: 2026, scopeCount: 2, recordCountPerScope: 6, clusterCount: 2);
        var caseSet = corpus.BuildEvalCaseSet();

        Console.WriteLine($"Corpus: {corpus.TotalRecords} records / {corpus.Scopes.Count} scopes, " +
                          $"{caseSet.Cases.Count} golden cases, determinism hash: {corpus.DeterminismHash()[..16]}…");

        var runner = new EvalRunner(new DefaultEvalServiceFactory(corpus.VectorDim));
        EvalReport report = await runner.RunAsync(corpus, caseSet, new EvalRunnerOptions(topK: 10))
            .ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"Aggregate over {report.Cases.Count} cases (top-k = {report.TopK}):");
        Console.WriteLine($"  Recall@k    = {report.Aggregate.RecallAtK:0.000}");
        Console.WriteLine($"  Precision@k = {report.Aggregate.PrecisionAtK:0.000}");
        Console.WriteLine($"  MRR         = {report.Aggregate.Mrr:0.000}");
        Console.WriteLine($"  NDCG@k      = {report.Aggregate.NdcgAtK:0.000}");
        Console.WriteLine($"  Thresholds pass: {report.AllThresholdsPass}");
        Console.WriteLine();
        Console.WriteLine("First three cases:");
        foreach (var evalCase in report.Cases.Take(3))
        {
            Console.WriteLine(
                $"  {evalCase.CaseId} ({evalCase.ScopeId}): recall={evalCase.RecallAtK:0.000} mrr={evalCase.Mrr:0.000} hits={evalCase.ResultCount}");
        }
    }
}
