using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Examples;

/// <summary>
/// Hybrid retrieval (BM25-style lexical + dense vector, fused with Reciprocal Rank Fusion):
/// a keyword-only hit and a semantic-only hit answer different halves of the corpus —
/// the fused query surfaces both.
/// </summary>
public sealed class Ex03_HybridSearch : ExampleBase
{
    public override async Task RunAsync()
    {
        var rig = new ExampleRig();
        var root = rig.FreshRoot("ex03");

        IMemoryService memory = rig.BuildService(root);
        var scope = new MemoryScope(MemoryScopeKind.Session, "baking-notes");

        // Keyword-only record: carries the rare literal token but is semantically distant.
        await memory.RememberAsync(new MemoryRecord(
            "keyword-only", scope, "keyword-only",
            "banneton inventory spreadsheet columns and pricing notes")).ConfigureAwait(false);
        // Semantic-only record: cluster terms (sourdough domain) but never the rare keyword.
        await memory.RememberAsync(new MemoryRecord(
            "semantic-only", scope, "semantic-only",
            "sourdough starter ferment schedule crumb oven spring")).ConfigureAwait(false);
        // Filler so the ranks are non-trivial.
        await memory.RememberAsync(new MemoryRecord(
            "filler", scope, "filler",
            "team standup notes about sprint planning and the offsite")).ConfigureAwait(false);

        await memory.BuildIndexesAsync(scope).ConfigureAwait(false);

        await PrintPass(memory, scope, "keyword query: 'banneton'", "banneton");
        await PrintPass(memory, scope, "semantic query: 'sourdough starter crumb'", "sourdough starter crumb");
        await PrintPass(memory, scope, "fused query: 'banneton sourdough crumb'", "banneton sourdough crumb");

        Console.WriteLine("The keyword-focused pass surfaces 'keyword-only', the meaning-focused pass");
        Console.WriteLine("surfaces 'semantic-only'; the fused query's combined ranking catches BOTH.");
    }

    private static async Task PrintPass(IMemoryService memory, MemoryScope scope, string label, string query)
    {
        var result = await memory.RecallAsync(new RetrievalQuery(query, scope, TopK: 3)).ConfigureAwait(false);
        Console.WriteLine($"-- {label}");
        foreach (var hit in result.Records)
        {
            var stages = string.Join(", ", hit.Provenance.StageScores.Select(pair => $"{pair.Key}={pair.Value:0.000}"));
            Console.WriteLine($"   {hit.Score:0.0000}  {hit.Record.Id,-14} [{hit.Provenance.Retriever}] {stages}");
        }
    }
}
