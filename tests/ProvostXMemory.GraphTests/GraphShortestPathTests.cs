using System;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Graph;
using ProvostXMemory.Storage;

using Xunit;

namespace ProvostXMemory.GraphTests;

/// <summary>BFS shortest path: direct edge, 2-hop path, disconnected nodes, self path.</summary>
public sealed class GraphShortestPathTests
{
    [Fact]
    public async Task Direct_edge_yields_two_node_path()
    {
        var store = await BuildFixtureAsync();
        var path = await store.ShortestPathAsync("alice-smith", "bob-jones");

        Assert.Equal(new[] { "alice-smith", "bob-jones" }, path.Select(e => e.EntityId));
    }

    [Fact]
    public async Task Two_hop_path_preferred_over_longer_route()
    {
        var store = await BuildFixtureAsync();

        // alice—dave exists directly, but alice→bob→carol→dave (3) vs alice—dave (1): direct wins.
        var direct = await store.ShortestPathAsync("alice-smith", "dave-miller");
        Assert.Equal(new[] { "alice-smith", "dave-miller" }, direct.Select(e => e.EntityId));

        // carol—dave direct; also carol→bob→alice→dave (3): direct wins.
        var carolDave = await store.ShortestPathAsync("carol-white", "dave-miller");
        Assert.Equal(new[] { "carol-white", "dave-miller" }, carolDave.Select(e => e.EntityId));

        // ernest connects only via carol: alice→bob→carol→ernest.
        var path = await store.ShortestPathAsync("alice-smith", "ernest-blue");
        Assert.Equal(new[] { "alice-smith", "bob-jones", "carol-white", "ernest-blue" }, path.Select(e => e.EntityId));
    }

    [Fact]
    public async Task Disconnected_entities_yield_empty_path()
    {
        var store = await BuildFixtureAsync();
        var path = await store.ShortestPathAsync("alice-smith", "zoe-island");
        Assert.Empty(path);
    }

    [Fact]
    public async Task Same_entity_yields_single_node_path()
    {
        var store = await BuildFixtureAsync();
        var path = await store.ShortestPathAsync("alice-smith", "alice-smith");
        Assert.Equal(new[] { "alice-smith" }, path.Select(e => e.EntityId));
    }

    [Fact]
    public async Task Neighbors_return_strongest_supported_first()
    {
        var store = await BuildFixtureAsync();

        // alice: 3 shared records with dave, 1 with bob → dave first.
        var neighbors = await store.NeighborsAsync("alice-smith", 10);
        Assert.Equal("dave-miller", neighbors[0].EntityId);
        Assert.Equal("bob-jones", neighbors[1].EntityId);
        Assert.Equal(2, neighbors.Count);
    }

    /// <summary>Fixture: alice—bob(1), bob—carol(2), carol—dave(1), alice—dave(3), carol—ernest(1); zoe isolated.</summary>
    private static async Task<JsonlMemoryGraphStore> BuildFixtureAsync()
    {
        var store = BuildStore();

        foreach (var name in new[] { "Alice Smith", "Bob Jones", "Carol White", "Dave Miller", "Ernest Blue", "Zoe Island" })
        {
            var id = Slug(name);
            await store.UpsertEntityAsync(new GraphEntity(id, name, Array.Empty<string>(), Array.Empty<string>()));
        }

        await store.UpsertRelationAsync(new GraphRelation("alice-smith+bob-jones", "alice-smith", "bob-jones", "co-occurs", new[] { "r1" }));
        await store.UpsertRelationAsync(new GraphRelation("bob-jones+carol-white", "bob-jones", "carol-white", "co-occurs", new[] { "r2", "r3" }));
        await store.UpsertRelationAsync(new GraphRelation("carol-white+dave-miller", "carol-white", "dave-miller", "co-occurs", new[] { "r4" }));
        await store.UpsertRelationAsync(new GraphRelation("alice-smith+dave-miller", "alice-smith", "dave-miller", "co-occurs", new[] { "r5", "r6", "r7" }));
        await store.UpsertRelationAsync(new GraphRelation("carol-white+ernest-blue", "carol-white", "ernest-blue", "co-occurs", new[] { "r8" }));
        return store;
    }

    private static JsonlMemoryGraphStore BuildStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "pvmx-graph-path-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new JsonlMemoryGraphStore(new MemoryRootLayout(root, "model-hash-v1"), "session", "fixture");
    }

    // Pure test-side slug helper mirroring extractor entity ids.
    private static string Slug(string name)
        => name.ToLowerInvariant().Replace(' ', '-');
}