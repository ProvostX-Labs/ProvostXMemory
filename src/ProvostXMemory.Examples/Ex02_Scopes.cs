using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Examples;

/// <summary>
/// Multi-tenant isolation: the SAME record key is written into three tenant scopes with
/// different content; recall in each scope only ever sees that scope's own record, and
/// lookups from a foreign scope find nothing.
/// </summary>
public sealed class Ex02_Scopes : ExampleBase
{
    public override async Task RunAsync()
    {
        var rig = new ExampleRig();
        var root = rig.FreshRoot("ex02");

        IMemoryService memory = rig.BuildService(root);
        var tenants = new[]
        {
            new MemoryScope(MemoryScopeKind.Tenant, "tenant-alpha"),
            new MemoryScope(MemoryScopeKind.Tenant, "tenant-beta"),
            new MemoryScope(MemoryScopeKind.Tenant, "tenant-gamma"),
        };

        // Same key "preferences" in every scope, different content per tenant.
        foreach (var tenant in tenants)
        {
            await memory.RememberAsync(new MemoryRecord(
                "preferences",
                tenant,
                "preferences",
                $"theme preference for {tenant.Id}: dark mode with high contrast")).ConfigureAwait(false);
            await memory.BuildIndexesAsync(tenant).ConfigureAwait(false);
        }

        var query = "theme preference";
        Console.WriteLine("Same key 'preferences' written into three tenant scopes.");
        foreach (var tenant in tenants)
        {
            var result = await memory.RecallAsync(new RetrievalQuery(query, tenant, TopK: 10))
                .ConfigureAwait(false);
            var ids = string.Join(", ", result.Records.Select(hit => hit.Record.Id));
            var scopes = string.Join(", ", result.Records.Select(hit => hit.Record.Scope.Id).Distinct());
            Console.WriteLine($"  recall in {tenant.Id,-13} → hits: [{ids}]  (scopes seen: {scopes})");
        }

        // Cross-scope proof: a scope that never stored the key sees nothing of the others.
        var outsider = new MemoryScope(MemoryScopeKind.Tenant, "tenant-outsider");
        var stranger = await memory.GetAsync(outsider, "preferences").ConfigureAwait(false);
        var outsiderRecall = await memory.RecallAsync(new RetrievalQuery(query, outsider, TopK: 10))
            .ConfigureAwait(false);
        Console.WriteLine($"  GetAsync from 'tenant-outsider' → {(stranger is null ? "null (isolated)" : "LEAK!")}");
        Console.WriteLine($"  recall in 'tenant-outsider'     → {outsiderRecall.Records.Count} hits (isolated)");
    }
}
