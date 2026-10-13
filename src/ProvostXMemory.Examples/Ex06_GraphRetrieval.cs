using ProvostXMemory.Abstractions;
using ProvostXMemory.Graph;
using ProvostXMemory.Storage;

namespace ProvostXMemory.Examples;

/// <summary>
/// Graph retrieval: records mentioning shared entities form an entity/relation graph at
/// index time (heuristic extractor). A graph-connected recall through the entity subgraph
/// surfaces ALL records touching an entity — including records the plain lexical/dense
/// query would miss because they share no query token.
/// </summary>
public sealed class Ex06_GraphRetrieval : ExampleBase
{
    public override async Task RunAsync()
    {
        var rig = new ExampleRig();
        var root = rig.FreshRoot("ex06");
        var embedder = rig.CreateEmbedder();
        var layout = new MemoryRootLayout(root, embedder.ModelHash);

        IMemoryService memory = rig.BuildService(root, embedder, graph: true);
        var scope = new MemoryScope(MemoryScopeKind.Session, "team-notes");

        // Three records mentioning the same person; one record sharing the topic instead.
        var records = new List<MemoryRecord>
        {
            new("dave-kube", scope, "dave-kube", "Dave Miller reviewed the kubernetes rollout plan."),
            new("dave-pg", scope, "dave-pg", "Dave Miller approved the postgres migration window."),
            new("dave-budget", scope, "dave-budget", "Dave Miller signed off the quarterly budget."),
            new("kube-only", scope, "kube-only", "The kubernetes rollout finished on staging successfully."),
        };
        foreach (var record in records)
        {
            await memory.RememberAsync(record).ConfigureAwait(false);
        }

        await memory.BuildIndexesAsync(scope).ConfigureAwait(false);

        // Plain recall: pure lexical/dense over query tokens.
        var plain = await memory.RecallAsync(new RetrievalQuery("budget", scope, TopK: 10)).ConfigureAwait(false);
        Console.WriteLine($"Plain recall \"budget\"              → [{string.Join(", ", plain.Records.Select(hit => hit.Record.Id))}]");

        // Graph-connected recall: entity match seeds a neighbor walk over the subgraph.
        var graphRetriever = new GraphRetriever(
            storeFactory: graphScope => new JsonlMemoryGraphStore(layout, graphScope),
            recordsProvider: (graphScope, _) =>
                Task.FromResult<IReadOnlyList<MemoryRecord>>(
                    graphScope.Id == scope.Id ? records.ToList() : new List<MemoryRecord>()));
        var connected = await graphRetriever.RetrieveAsync("dave miller", scope, 10).ConfigureAwait(false);
        Console.WriteLine($"Graph recall \"dave miller\" (entity) → [{string.Join(", ", connected.Select(hit => hit.Record.Id))}]");
        Console.WriteLine();
        Console.WriteLine("The entity 'dave-miller' links all three of Dave's records; the graph walk");
        Console.WriteLine("surfaces records connected through him even without matching their tokens.");
    }
}
