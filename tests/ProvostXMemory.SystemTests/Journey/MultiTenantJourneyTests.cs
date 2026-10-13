using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Fusion;
using ProvostXMemory.VectorIndex;

using Xunit;

namespace ProvostXMemory.SystemTests.Journey;

/// <summary>
/// THE multi-tenant journey: three scopes of different kinds (platform root, session
/// child, tenant child), interleaved writes, then per-scope queries that must return
/// ONLY their own records; forget/update operations respect scope boundaries; and no
/// state leaks across scopes anywhere in the journey — all on the public
/// IMemoryService surface.
/// </summary>
public sealed class MultiTenantJourneyTests : IDisposable
{
    private readonly TempRoot _root = new("journey-multitenant");

    public void Dispose() => _root.Dispose();

    private static (MemoryScope Platform, MemoryScope Tenant, MemoryScope Session) Scopes()
    {
        var platform = new MemoryScope(MemoryScopeKind.Platform, "platform-core");
        var tenant = new MemoryScope(MemoryScopeKind.Tenant, "tenant-acme", platform);
        var session = new MemoryScope(MemoryScopeKind.Session, "session-42", tenant);
        return (platform, tenant, session);
    }

    private static MemoryRecord Record(MemoryScope scope, string id, string content, string[]? tags = null) =>
        new(id, scope, id, content) { Tags = tags ?? Array.Empty<string>() };

    /// <summary>Kind+id identity: storage routes by kind/id only (parent chains are metadata).</summary>
    private static string ScopeKey(MemoryScope scope) => $"{scope.Kind}/{scope.Id}";

    [Fact]
    public async Task Interleaved_writes_stay_isolated_and_mutations_respect_boundaries()
    {
        var (platform, tenant, session) = Scopes();
        var service = SystemRig.BuildService(_root.Path_);

        // ---- Interleaved writes across all three scopes ----
        var platformRecords = new[]
        {
            Record(platform, "plat-1", "platform billing meter registry config"),
            Record(platform, "plat-2", "platform feature flag rollout engine", new[] { "flags" }),
        };
        var tenantRecords = new[]
        {
            Record(tenant, "tnnt-1", "tenant acme invoice stripe refund policy"),
            Record(tenant, "tnnt-2", "tenant acme sso saml certificate rotation", new[] { "sso" }),
        };
        var sessionRecords = new[]
        {
            Record(session, "sess-1", "session user kubernetes helm rollout draft"),
            Record(session, "sess-2", "session user sourdough starter ferment scratchpad"),
        };
        foreach (var record in platformRecords.Concat(tenantRecords).Concat(sessionRecords))
        {
            await service.RememberAsync(record);
        }
        await service.BuildIndexesAsync(platform);
        await service.BuildIndexesAsync(tenant);
        await service.BuildIndexesAsync(session);

        // ---- Per-scope queries return ONLY their own records ----
        var platformQuery = await service.RecallAsync(new RetrievalQuery("billing meter registry rollout", platform, 6));
        Assert.NotEmpty(platformQuery.Records);
        Assert.All(platformQuery.Records, hit => Assert.True(ScopeKey(platform) == ScopeKey(hit.Record.Scope)));
        Assert.Contains(platformQuery.Records, hit => hit.Record.Id == "plat-1");

        var tenantQuery = await service.RecallAsync(new RetrievalQuery("invoice stripe refund certificate", tenant, 6));
        Assert.NotEmpty(tenantQuery.Records);
        Assert.All(tenantQuery.Records, hit => Assert.True(ScopeKey(tenant) == ScopeKey(hit.Record.Scope)));
        Assert.Contains(tenantQuery.Records, hit => hit.Record.Id == "tnnt-1");

        var sessionQuery = await service.RecallAsync(new RetrievalQuery("kubernetes helm rollout sourdough", session, 6));
        Assert.NotEmpty(sessionQuery.Records);
        Assert.All(sessionQuery.Records, hit => Assert.True(ScopeKey(session) == ScopeKey(hit.Record.Scope)));
        Assert.Contains(sessionQuery.Records, hit => hit.Record.Id == "sess-1");

        // Shared vocabulary ("rollout") must not leak tenant/session rows into platform results.
        Assert.DoesNotContain(platformQuery.Records, hit => hit.Record.Id.StartsWith("tnnt-", StringComparison.Ordinal));
        Assert.DoesNotContain(platformQuery.Records, hit => hit.Record.Id.StartsWith("sess-", StringComparison.Ordinal));
        Assert.DoesNotContain(tenantQuery.Records, hit => hit.Record.Id.StartsWith("plat-", StringComparison.Ordinal));
        Assert.DoesNotContain(sessionQuery.Records, hit => hit.Record.Id.StartsWith("tnnt-", StringComparison.Ordinal));

        // ---- Get/Update respect scope boundaries ----
        var crossGet = await service.GetAsync(session, "tnnt-1"); // different scope, foreign id
        Assert.Null(crossGet);
        Assert.NotNull(await service.GetAsync(tenant, "tnnt-1")); // own scope sees it

        await service.UpdateAsync(Record(tenant, "tnnt-1", "tenant acme invoice stripe refund policy updated"));
        var updatedInTenant = await service.GetAsync(tenant, "tnnt-1");
        Assert.NotNull(updatedInTenant);
        Assert.Contains("updated", updatedInTenant.Content, StringComparison.Ordinal);

        // ---- Forget respects scope boundaries ----
        await service.ForgetAsync(tenant, "tnnt-2");
        Assert.Null(await service.GetAsync(tenant, "tnnt-2"));
        await service.BuildIndexesAsync(tenant);
        var afterForget = await service.RecallAsync(new RetrievalQuery("sso saml certificate rotation", tenant, 6));
        Assert.DoesNotContain(afterForget.Records, hit => hit.Record.Id == "tnnt-2");
        // ...and nothing else vanished (sibling intact; other scopes untouched).
        Assert.NotNull(await service.GetAsync(tenant, "tnnt-1"));
        Assert.NotNull(await service.GetAsync(session, "sess-2"));
        Assert.NotNull(await service.GetAsync(platform, "plat-2"));

        // ---- Full-journey state: every scope still resolves exactly its own records ----
        foreach (var (scopeKind, prefix) in new[] { (platform, "plat-"), (tenant, "tnnt-"), (session, "sess-") })
        {
            var everything = await service.RecallAsync(new RetrievalQuery("acme registry sso kubernetes sourdough billing", scopeKind, 10));
            Assert.All(everything.Records, hit => Assert.True(ScopeKey(scopeKind) == ScopeKey(hit.Record.Scope)));
        }

        // Scopes() enumerates all three after the whole journey.
        var scopes = service.Scopes();
        Assert.Contains(scopes, s => s.Kind == MemoryScopeKind.Platform && s.Id == platform.Id);
        Assert.Contains(scopes, s => s.Kind == MemoryScopeKind.Tenant && s.Id == tenant.Id);
        Assert.Contains(scopes, s => s.Kind == MemoryScopeKind.Session && s.Id == session.Id);
    }

    /// <summary>Restart mid-journey: scope isolation survives (store-backed, rebuilt).</summary>
    [Fact]
    public async Task Scope_isolation_survives_a_restart()
    {
        var (platform, tenant, session) = Scopes();
        var seed = SystemRig.BuildService(_root.Path_);
        foreach (var record in new[]
                 {
                     Record(platform, "plat-1", "platform billing meter registry config"),
                     Record(tenant, "tnnt-1", "tenant acme invoice stripe refund policy"),
                     Record(session, "sess-1", "session user kubernetes helm rollout draft"),
                 })
        {
            await seed.RememberAsync(record);
        }
        await seed.BuildIndexesAsync(platform);
        await seed.BuildIndexesAsync(tenant);
        await seed.BuildIndexesAsync(session);

        var restarted = SystemRig.AttachService(_root.Path_);
        await restarted.BuildIndexesAsync(platform);
        await restarted.BuildIndexesAsync(tenant);
        await restarted.BuildIndexesAsync(session);

        foreach (var (scopeKind, ownId) in new[] { (platform, "plat-1"), (tenant, "tnnt-1"), (session, "sess-1") })
        {
            var recall = await restarted.RecallAsync(new RetrievalQuery("billing invoice registry kubernetes ferment", scopeKind, 6));
            Assert.All(recall.Records, hit => Assert.True(ScopeKey(scopeKind) == ScopeKey(hit.Record.Scope)));
            Assert.Contains(recall.Records, hit => hit.Record.Id == ownId);
        }
    }
}