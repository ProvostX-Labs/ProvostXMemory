using ProvostXMemory.Abstractions;
using ProvostXMemory.Consolidation;
using ProvostXMemory.Storage;

namespace ProvostXMemory.Examples;

/// <summary>
/// Consolidation: near-duplicate records are merged into one survivor plus a cluster
/// summary record; TTL-expired records are deleted. Before/after counts and the summary
/// record are printed.
/// </summary>
public sealed class Ex05_Consolidation : ExampleBase
{
    public override async Task RunAsync()
    {
        var rig = new ExampleRig();
        var root = rig.FreshRoot("ex05");
        var layout = new MemoryRootLayout(root, rig.CreateEmbedder().ModelHash);
        var store = new JsonlMemoryStore(layout);

        IMemoryService memory = rig.BuildService(root, consolidation: new ConsolidationOptions(
            DedupSimilarityThreshold: 0.92,
            ExpireTtl: true,
            EmitClusterSummaries: true));
        var scope = new MemoryScope(MemoryScopeKind.Session, "cleanup-demo");
        var scopeKind = scope.Kind.ToString();

        // Cluster seed kept: hub joins both of the next two records (cosine ≈ 0.87 with
        // the deterministic embedder — similar, but below the 0.92 dedup threshold).
        await memory.RememberAsync(new MemoryRecord(
            "hub-1", scope, "rollout-hub",
            "kubernetes rollout helm chart")).ConfigureAwait(false);
        await memory.RememberAsync(new MemoryRecord(
            "rel-1", scope, "rollout-brief",
            "kubernetes rollout")).ConfigureAwait(false);
        await memory.RememberAsync(new MemoryRecord(
            "rel-2", scope, "helm-release",
            "helm chart release pinned to the stable repository")).ConfigureAwait(false);
        // A true near-duplicate pair (cosine ≈ 0.99 ≥ 0.92): one survivor + one deleted.
        await memory.RememberAsync(new MemoryRecord(
            "deploy-1", scope, "deploy-checklist",
            "deploy checklist build test ship release tag")).ConfigureAwait(false);
        await memory.RememberAsync(new MemoryRecord(
            "deploy-2", scope, "deploy-checklist",
            "deploy checklist build test ship release tag step")).ConfigureAwait(false);
        // Two expired records (TTL already elapsed).
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
        await memory.RememberAsync(new MemoryRecord(
            "stale-1", scope, "stale-cache-note",
            "temporary cache warming note") { ExpiresAt = yesterday }).ConfigureAwait(false);
        await memory.RememberAsync(new MemoryRecord(
            "stale-2", scope, "stale-lock-note",
            "temporary advisory lock reminder") { ExpiresAt = yesterday }).ConfigureAwait(false);
        // One distinct healthy record that must survive untouched.
        await memory.RememberAsync(new MemoryRecord(
            "keep-1", scope, "postgres-index",
            "postgres migration added the audit index")).ConfigureAwait(false);
        var before = await store.ListAsync(scopeKind, scope.Id).ConfigureAwait(false);
        Console.WriteLine($"Before consolidation: {before.Count} records");
        foreach (var record in before)
        {
            Console.WriteLine($"  {record.Id,-10} expires: {(record.ExpiresAt?.ToString("yyyy-MM-dd") ?? "-")}");
        }

        await memory.ConsolidateAsync(scope, new ConsolidationOptions(
            DedupSimilarityThreshold: 0.92,
            ExpireTtl: true,
            EmitClusterSummaries: true)).ConfigureAwait(false);

        var after = await store.ListAsync(scopeKind, scope.Id).ConfigureAwait(false);
        var survivors = after.Where(record => record.Category != ClusterSummarizer.SummaryCategory).ToList();
        var summaries = after.Where(record => record.Category == ClusterSummarizer.SummaryCategory).ToList();
        var removedIds = before.Select(record => record.Id).Except(survivors.Select(record => record.Id)).ToList();

        Console.WriteLine();
        Console.WriteLine($"After consolidation:  {after.Count} records ({survivors.Count} originals + {summaries.Count} summary)");
        Console.WriteLine($"Removed: {removedIds.Count} (expired TTL + near-duplicate losers): [{string.Join(", ", removedIds)}]");
        foreach (var summary in summaries)
        {
            var shortId = summary.Id.Length > 24 ? summary.Id[..24] + "…" : summary.Id;
            Console.WriteLine($"Summary record {shortId} (cluster members: {summary.Meta.GetValueOrDefault("cluster-member-count")}):");
            Console.WriteLine($"  {summary.Content.ReplaceLineEndings(" | ")}");
        }

        Console.WriteLine();
        Console.WriteLine("The near-duplicate pair collapsed to one survivor, the 3-member similar cluster");
        Console.WriteLine("emitted a cluster summary record, and the expired records were deleted.");
    }
}
