using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Examples;

/// <summary>
/// Minimal end-to-end flow (the README quickstart, made runnable): build a service,
/// remember five facts, build indexes, recall, and print the ranked hits.
/// </summary>
public sealed class Ex01_Quickstart : ExampleBase
{
    public override async Task RunAsync()
    {
        var rig = new ExampleRig();
        var root = rig.FreshRoot("ex01");
        var embedder = rig.CreateEmbedder();

        IMemoryService memory = rig.BuildService(root, embedder);
        var scope = new MemoryScope(MemoryScopeKind.Session, "user-42");

        // Five facts about two topical clusters (kubernetes ops and postgres ops).
        var facts = new[]
        {
            ("fact-1", "kubernetes cluster rollout completed without downtime"),
            ("fact-2", "helm chart release pinned to the stable repository"),
            ("fact-3", "postgres migration added the audit index overnight"),
            ("fact-4", "vacuum reclaimed tablespace after the migration"),
            ("fact-5", "rollout canary compared latency across two regions"),
        };
        foreach (var (id, content) in facts)
        {
            await memory.RememberAsync(new MemoryRecord(id, scope, id, content)).ConfigureAwait(false);
        }

        // Indexes must be built (or refreshed) before dense/lexical retrieval sees the records.
        await memory.BuildIndexesAsync(scope).ConfigureAwait(false);

        var result = await memory.RecallAsync(new RetrievalQuery(
            "kubernetes rollout",
            scope,
            TopK: 5)).ConfigureAwait(false);

        Console.WriteLine($"Root: ./memory-root (temp), scope: {scope.Kind}/{scope.Id}");
        Console.WriteLine($"Query: \"kubernetes rollout\" → {result.Records.Count} hits");
        foreach (var hit in result.Records)
        {
            Console.WriteLine($"  {hit.Score:0.0000}  {hit.Record.Id}: {hit.Record.Content}");
        }
    }
}
