using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Examples;

/// <summary>
/// Restart persistence: records written through the service survive disposal — a brand
/// new service instance rebuilt over the same root recalls them, proving the transactional
/// JSONL store is the durable source of truth (indexes are rebuilt from it).
/// </summary>
public sealed class Ex10_RestartPersistence : ExampleBase
{
    public override async Task RunAsync()
    {
        var rig = new ExampleRig();
        var root = rig.FreshRoot("ex10");
        var scope = new MemoryScope(MemoryScopeKind.Session, "durable-notes");

        string beforeIds;
        {
            IMemoryService first = rig.BuildService(root);
            await first.RememberAsync(new MemoryRecord(
                "persist-1", scope, "rollout-note",
                "kubernetes rollout completed without downtime")).ConfigureAwait(false);
            await first.RememberAsync(new MemoryRecord(
                "persist-2", scope, "migration-note",
                "postgres migration added the audit index")).ConfigureAwait(false);
            await first.BuildIndexesAsync(scope).ConfigureAwait(false);

            var recallBefore = await first.RecallAsync(new RetrievalQuery("kubernetes rollout", scope, TopK: 5))
                .ConfigureAwait(false);
            beforeIds = string.Join(", ", recallBefore.Records.Select(hit => hit.Record.Id));
            Console.WriteLine($"Before restart: recall \"kubernetes rollout\" → [{beforeIds}]");
            // "Disposal": drop the reference; the JSONL store already persisted every write.
        }

        // Rebuild: identical wiring, brand new instances, SAME root.
        IMemoryService second = rig.BuildService(root);
        await second.BuildIndexesAsync(scope).ConfigureAwait(false); // indexes rebuild from the store

        var recallAfter = await second.RecallAsync(new RetrievalQuery("kubernetes rollout", scope, TopK: 5))
            .ConfigureAwait(false);
        var afterIds = string.Join(", ", recallAfter.Records.Select(hit => hit.Record.Id));
        var fetched = await second.GetAsync(scope, "persist-2").ConfigureAwait(false);

        Console.WriteLine($"After  restart: recall \"kubernetes rollout\" → [{afterIds}]");
        Console.WriteLine($"GetAsync('persist-2') after restart: {(fetched is null ? "MISSING" : $"'{fetched.Content}'")}");
        Console.WriteLine();
        Console.WriteLine(beforeIds == afterIds
            ? "Persistence proven: identical recall before and after a full service restart."
            : "UNEXPECTED: recall differed across restart!");
    }
}
