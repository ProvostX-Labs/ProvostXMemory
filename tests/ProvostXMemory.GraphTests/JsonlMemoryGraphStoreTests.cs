using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Graph;
using ProvostXMemory.Storage;

using Xunit;

namespace ProvostXMemory.GraphTests;

/// <summary>Store round-trip, merge semantics and scope isolation on temp-root fixtures.</summary>
public sealed class JsonlMemoryGraphStoreTests
{
    [Fact]
    public async Task Entity_round_trips_through_entities_jsonl()
    {
        var root = NewRoot();
        try
        {
            var store = NewStore(root, "tenant", "acme");
            var entity = new GraphEntity(
                "frank-lloyd-wright",
                "Frank Lloyd Wright",
                new[] { "FLW" },
                new[] { "rec-1" });

            await store.UpsertEntityAsync(entity);

            var loaded = await store.GetEntityAsync("frank-lloyd-wright");
            Assert.NotNull(loaded);
            Assert.Equal("frank-lloyd-wright", loaded!.EntityId);
            Assert.Equal("Frank Lloyd Wright", loaded.Name);
            Assert.Equal(new[] { "FLW" }, loaded.Aliases);
            Assert.Equal(new[] { "rec-1" }, loaded.RecordIds);

            var serialized = File.ReadAllText(store.EntitiesPath);
            Assert.Contains("frank-lloyd-wright", serialized);
            Assert.Single(serialized.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task Repeated_entity_upserts_accumulate_record_ids_and_keep_name()
    {
        var root = NewRoot();
        try
        {
            var store = NewStore(root, "tenant", "acme");

            await store.UpsertEntityAsync(new GraphEntity("alice-smith", "Alice Smith", Array.Empty<string>(), new[] { "rec-1" }));
            await store.UpsertEntityAsync(new GraphEntity("alice-smith", "WRONG", Array.Empty<string>(), new[] { "rec-2" }));

            var loaded = await store.GetEntityAsync("alice-smith");
            Assert.NotNull(loaded);
            Assert.Equal("Alice Smith", loaded!.Name); // first canonical name wins
            Assert.Equal(new[] { "rec-1", "rec-2" }, loaded.RecordIds);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task Repeated_relation_upserts_merge_by_unordered_pair_and_label()
    {
        var root = NewRoot();
        try
        {
            var store = NewStore(root, "tenant", "acme");

            await store.UpsertRelationAsync(new GraphRelation("a+b", "a", "b", "co-occurs", new[] { "rec-1" }));
            // Reverse direction pair is recognized as the same relation.
            await store.UpsertRelationAsync(new GraphRelation("b+a", "b", "a", "co-occurs", new[] { "rec-2" }));
            // A different label is a distinct relation.
            await store.UpsertRelationAsync(new GraphRelation("a+b-collab", "a", "b", "collaborates", new[] { "rec-3" }));

            var relationsOfA = await store.RelationsOfAsync("a", 10);
            Assert.Equal(2, relationsOfA.Count);

            var coOccurrence = relationsOfA.Single(r => r.Label == "co-occurs");
            Assert.Equal(new[] { "a", "b" }, new[] { coOccurrence.FromEntityId, coOccurrence.ToEntityId }.OrderBy(x => x, StringComparer.Ordinal));
            Assert.Equal(new[] { "rec-1", "rec-2" }, coOccurrence.RecordIds);

            var serialized = File.ReadAllText(store.RelationsPath);
            Assert.Equal(2, serialized.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task Cluster_of_returns_cluster_containing_entity()
    {
        var root = NewRoot();
        try
        {
            var store = NewStore(root, "tenant", "acme");
            await store.UpsertEntityAsync(new GraphEntity("a", "A", Array.Empty<string>(), Array.Empty<string>()));
            await store.UpsertEntityAsync(new GraphEntity("b", "B", Array.Empty<string>(), Array.Empty<string>()));
            await WriteClusterLineAsync(store, new GraphCluster("cluster-1", new[] { "a", "b" }, "summary-rec-1"));
            await WriteClusterLineAsync(store, new GraphCluster("cluster-2", new[] { "c" }, null));

            var clusterOfA = await store.ClusterOfAsync("a");
            Assert.NotNull(clusterOfA);
            Assert.Equal("cluster-1", clusterOfA!.ClusterId);
            Assert.Equal("summary-rec-1", clusterOfA.SummaryRecordId);

            var clusterOfC = await store.ClusterOfAsync("c");
            Assert.NotNull(clusterOfC);
            Assert.Null(clusterOfC!.SummaryRecordId);

            var clusterOfMissing = await store.ClusterOfAsync("zzz");
            Assert.Null(clusterOfMissing);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task Store_instance_is_pinned_to_scope_and_cannot_read_other_scope()
    {
        var root = NewRoot();
        try
        {
            var storeA = NewStore(root, "tenant", "acme");
            var storeB = NewStore(root, "tenant", "globex");

            await storeA.UpsertEntityAsync(new GraphEntity("shared-id", "A Only", Array.Empty<string>(), new[] { "rec-a" }));
            await storeB.UpsertEntityAsync(new GraphEntity("shared-id", "B Only", Array.Empty<string>(), new[] { "rec-b" }));

            // Structural isolation: each pinned instance resolves paths only in its own scope dir.
            var fromA = await storeA.GetEntityAsync("shared-id");
            var fromB = await storeB.GetEntityAsync("shared-id");
            Assert.Equal("A Only", fromA!.Name);
            Assert.Equal("B Only", fromB!.Name);

            Assert.Contains($"tenant{Path.DirectorySeparatorChar}acme{Path.DirectorySeparatorChar}graph", storeA.EntitiesPath);
            Assert.Contains($"tenant{Path.DirectorySeparatorChar}acme{Path.DirectorySeparatorChar}graph", storeA.RelationsPath);
            Assert.Contains($"tenant{Path.DirectorySeparatorChar}acme{Path.DirectorySeparatorChar}graph", storeA.ClustersPath);
            Assert.False(storeA.EntitiesPath.Contains("globex", StringComparison.Ordinal));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task Cluster_upsert_round_trips_and_replaces_same_cluster_id()
    {
        var root = NewRoot();
        try
        {
            var store = NewStore(root, "tenant", "acme");
            await store.UpsertEntityAsync(new GraphEntity("a", "A", Array.Empty<string>(), Array.Empty<string>()));
            await store.UpsertEntityAsync(new GraphEntity("b", "B", Array.Empty<string>(), Array.Empty<string>()));
            await store.UpsertEntityAsync(new GraphEntity("c", "C", Array.Empty<string>(), Array.Empty<string>()));

            await store.UpsertClusterAsync(new GraphCluster("cluster-1", new[] { "a", "b" }, "summary-1"));
            var first = await store.ClusterOfAsync("a");
            Assert.NotNull(first);
            Assert.Equal("cluster-1", first!.ClusterId);
            Assert.Equal("summary-1", first.SummaryRecordId);

            // Same cluster id → authoritative replacement, not accumulation.
            await store.UpsertClusterAsync(new GraphCluster("cluster-1", new[] { "b", "c" }, "summary-2"));
            var goneFromA = await store.ClusterOfAsync("a");
            Assert.Null(goneFromA);
            var movedToC = await store.ClusterOfAsync("c");
            Assert.NotNull(movedToC);
            Assert.Equal(new[] { "b", "c" }, movedToC!.EntityIds);
            Assert.Equal("summary-2", movedToC.SummaryRecordId);

            // Exactly one line remains, written through the transactional writer.
            var lines = (await File.ReadAllLinesAsync(store.ClustersPath))
                .Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
            Assert.Single(lines);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task Cluster_upsert_is_scope_bound_and_visible_through_the_store_api()
    {
        var root = NewRoot();
        try
        {
            var storeA = NewStore(root, "tenant", "acme");
            var storeB = NewStore(root, "tenant", "globex");

            await storeA.UpsertClusterAsync(new GraphCluster("shared-cluster", new[] { "a" }, "summary-a"));
            await storeB.UpsertClusterAsync(new GraphCluster("shared-cluster", new[] { "b" }, "summary-b"));

            var fromA = await storeA.ClusterOfAsync("a");
            Assert.Equal("summary-a", fromA!.SummaryRecordId);
            var fromB = await storeB.ClusterOfAsync("b");
            Assert.Equal("summary-b", fromB!.SummaryRecordId);

            // Shared-id cluster lines live in separate scope files.
            Assert.Equal(1, (await File.ReadAllLinesAsync(storeA.ClustersPath)).Count(line => !string.IsNullOrWhiteSpace(line)));
            Assert.Equal(1, (await File.ReadAllLinesAsync(storeB.ClustersPath)).Count(line => !string.IsNullOrWhiteSpace(line)));
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "pvmx-graph-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static JsonlMemoryGraphStore NewStore(string root, string scopeKind, string scopeId)
    {
        return new JsonlMemoryGraphStore(new MemoryRootLayout(root, "model-hash-v1"), scopeKind, scopeId);
    }

    private static async Task WriteClusterLineAsync(JsonlMemoryGraphStore store, GraphCluster cluster)
    {
        // The dedicated upsert API is the only sanctioned way to write clusters.
        await store.UpsertClusterAsync(cluster);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // best effort cleanup
        }
    }

    private sealed record ClusterDto(string ClusterId, System.Collections.Generic.IReadOnlyList<string> EntityIds, string? SummaryRecordId);
}