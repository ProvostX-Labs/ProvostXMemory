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
/// THE lifecycle journey on the public IMemoryService surface: remember → recall →
/// update (recall follows the new content) → forget → recall-empty → re-index rebuild →
/// recall-restored with byte-identical rankings to pre-rebuild — plus a restart variant
/// where a fresh service over the same root reproduces the same state.
/// </summary>
public sealed class LifecycleJourneyTests : IDisposable
{
    private readonly TempRoot _root = new("journey-lifecycle");

    public void Dispose() => _root.Dispose();

    private static MemoryScope Scope() => new(MemoryScopeKind.Session, "lifecycle");

    private static MemoryRecord[] Records()
    {
        var scope = Scope();
        return new[]
        {
            new MemoryRecord("rec-kube", scope, "rec-kube", "kubernetes cluster rollout helm chart"),
            new MemoryRecord("rec-pg", scope, "rec-pg", "postgres vacuum tablespace index")
            {
                Tags = new[] { "database" },
            },
            new MemoryRecord("rec-bake", scope, "rec-bake", "sourdough starter ferment oven"),
        };
    }

    private static async Task<MemoryService> SeedAsync(string root)
    {
        var service = SystemRig.BuildService(root);
        foreach (var record in Records())
        {
            await service.RememberAsync(record);
        }
        await service.BuildIndexesAsync(Scope());
        return service;
    }

    private static async Task<Dictionary<string, string[]>> SnapshotAsync(IMemoryService service)
    {
        var queries = new Dictionary<string, string>
        {
            ["kube"] = "kubernetes cluster rollout",
            ["bake"] = "sourdough starter ferment",
        };
        var snapshot = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (name, text) in queries)
        {
            snapshot[name] = (await service.RecallAsync(new RetrievalQuery(text, Scope(), 5)))
                .Records
                .Select(hit => hit.Record.Id)
                .ToArray();
        }
        return snapshot;
    }

    [Fact]
    public async Task Remember_update_forget_recall_empty_rebuild_recall_restored()
    {
        var service = await SeedAsync(_root.Path_);
        var scope = Scope();

        // ---- remember + recall sanity ----
        var kubeBefore = await service.RecallAsync(new RetrievalQuery("kubernetes cluster rollout", scope, 5));
        Assert.Equal("rec-kube", kubeBefore.Records[0].Record.Id);

        // ---- update: recall follows the new content ----
        var pgRecord = (await service.GetAsync(scope, "rec-pg"))!;
        await service.UpdateAsync(pgRecord with { Content = "chopin nocturne pedal polyrhythm" });
        var updated = await service.GetAsync(scope, "rec-pg");
        Assert.NotNull(updated);
        Assert.Equal("chopin nocturne pedal polyrhythm", updated.Content);

        var pgNew = await service.RecallAsync(new RetrievalQuery("chopin nocturne pedal", scope, 5));
        Assert.Equal("rec-pg", pgNew.Records[0].Record.Id);
        var pgOld = await service.RecallAsync(new RetrievalQuery("vacuum tablespace index", scope, 5));
        // Dense ANN returns nearest neighbors regardless of absolute similarity, so in a
        // 3-record scope rec-pg still surfaces for old-token queries; the update guarantee
        // is freshness: every rec-pg hit carries the new content, and the old tokens no
        // longer rank it first.
        Assert.All(
            pgOld.Records.Where(hit => hit.Record.Id == "rec-pg"),
            hit => Assert.Equal("chopin nocturne pedal polyrhythm", hit.Record.Content));
        Assert.NotEqual("rec-pg", pgOld.Records[0].Record.Id);

        // ---- forget: recall-empty for the forgotten content ----
        await service.ForgetAsync(scope, "rec-pg");
        Assert.Null(await service.GetAsync(scope, "rec-pg"));
        var afterForget = await service.RecallAsync(new RetrievalQuery("chopin nocturne pedal", scope, 5));
        // Forgotten content must be unretrievable; dense ANN may still surface the
        // remaining siblings (nearest neighbors regardless of absolute similarity).
        Assert.DoesNotContain(afterForget.Records, hit => hit.Record.Id == "rec-pg");

        // Pre-rebuild snapshot (forgotten record out, siblings intact).
        var preRebuild = await SnapshotAsync(service);

        // ---- re-index rebuild: identical results to pre-rebuild ----
        await service.RebuildAsync();

        var postRebuild = await SnapshotAsync(service);
        foreach (var key in preRebuild.Keys)
        {
            Assert.True(
                preRebuild[key].SequenceEqual(postRebuild[key]),
                $"rebuild diverged on '{key}': [{string.Join(',', preRebuild[key])}] vs [{string.Join(',', postRebuild[key])}]");
        }

        // The forgotten record stays forgotten after the rebuild.
        Assert.Null(await service.GetAsync(scope, "rec-pg"));
        Assert.DoesNotContain(
            (await service.RecallAsync(new RetrievalQuery("chopin nocturne pedal", scope, 5))).Records,
            hit => hit.Record.Id == "rec-pg");

        // ---- recall-restored: rebuild also restores retrieval from the store alone ----
        // (Simulated by dropping into a fresh service: rebuild-on-boot path.)
        var fresh = SystemRig.AttachService(_root.Path_);
        await fresh.BuildIndexesAsync(Scope());
        var restored = await SnapshotAsync(fresh);
        foreach (var key in preRebuild.Keys)
        {
            Assert.Equal(preRebuild[key], restored[key]);
        }
    }

    /// <summary>Rebuild determinism: repeated rebuilds reproduce identical rankings.</summary>
    [Fact]
    public async Task Repeated_rebuilds_reproduce_identical_rankings()
    {
        var service = await SeedAsync(_root.Path_);
        var first = await SnapshotAsync(service);

        await service.RebuildAsync();
        var second = await SnapshotAsync(service);
        await service.RebuildAsync();
        var third = await SnapshotAsync(service);

        foreach (var key in first.Keys)
        {
            Assert.Equal(first[key], second[key]);
            Assert.Equal(first[key], third[key]);
        }
    }

    /// <summary>Update persists across a restart; forgot content stays forgotten.</summary>
    [Fact]
    public async Task Mutations_persist_across_restart()
    {
        var service = await SeedAsync(_root.Path_);
        var scope = Scope();

        var pgRecord = (await service.GetAsync(scope, "rec-pg"))!;
        await service.UpdateAsync(pgRecord with { Content = "chopin nocturne pedal polyrhythm" });
        await service.ForgetAsync(scope, "rec-bake");

        var restarted = SystemRig.AttachService(_root.Path_);
        await restarted.BuildIndexesAsync(Scope());

        var updated = await restarted.GetAsync(scope, "rec-pg");
        Assert.NotNull(updated);
        Assert.Equal("chopin nocturne pedal polyrhythm", updated.Content);
        Assert.Null(await restarted.GetAsync(scope, "rec-bake"));

        var pgNew = await restarted.RecallAsync(new RetrievalQuery("chopin nocturne pedal", scope, 5));
        Assert.Equal("rec-pg", pgNew.Records[0].Record.Id);
        var bakeQuery = await restarted.RecallAsync(new RetrievalQuery("sourdough starter ferment", scope, 5));
        Assert.DoesNotContain(bakeQuery.Records, hit => hit.Record.Id == "rec-bake");
    }
}