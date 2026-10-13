using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Graph;
using ProvostXMemory.Storage;

using Xunit;

namespace ProvostXMemory.GraphTests;

/// <summary>Graph retriever: seed by entity/tag, hop decay ordering, cluster summaries, scope pinning.</summary>
public sealed class GraphRetrieverTests
{
    private const string ScopeId = "s1";

    [Fact]
    public async Task Direct_entity_match_outranks_second_hop_neighbor()
    {
        using var fixture = await RetrieverFixture.CreateAsync();
        var retriever = fixture.CreateRetriever(new GraphOptions(Enabled: true, MaxNeighbors: 8, MaxHops: 2));

        var results = await retriever.RetrieveAsync("alice", fixture.Scope, 5);

        Assert.NotEmpty(results);
        Assert.Equal("rec-alice-1", results[0].Record.Id); // hop-0 entity mention
        // Entity mention (1.0) + shared supporting relation evidence; must be at least the direct weight.
        Assert.True(results[0].Score >= 1.0, $"hop-0 score must be >= 1.0 but was {results[0].Score}");
        Assert.Equal("graph", results[0].Provenance.Retriever);
        Assert.Contains("rec-bob-1", results.Select(r => r.Record.Id)); // 1-hop neighbor mention
        Assert.True(results[0].Score > results[^1].Score, "hop-0 evidence must outrank later hops");
    }

    [Fact]
    public async Task Relation_support_accumulates_score_across_records()
    {
        using var fixture = await RetrieverFixture.CreateAsync();
        var retriever = fixture.CreateRetriever(new GraphOptions(Enabled: true, MaxNeighbors: 8, MaxHops: 3));

        var results = await retriever.RetrieveAsync("carol", fixture.Scope, 10);

        var daveHits = results.Where(r => r.Record.Id.StartsWith("rec-dave", StringComparison.Ordinal)).ToList();
        Assert.True(daveHits.Count >= 2, "relation-supported records must surface");
        var directCarol = results.First(r => r.Record.Id == "rec-carol-1");
        Assert.True(directCarol.Score > daveHits.Min(h => h.Score), "entity mention outranks relation-only evidence");
    }

    [Fact]
    public async Task Tag_match_seeds_record_at_hop_zero()
    {
        using var fixture = await RetrieverFixture.CreateAsync();
        var retriever = fixture.CreateRetriever(new GraphOptions(Enabled: true, MaxNeighbors: 8, MaxHops: 1));

        var results = await retriever.RetrieveAsync("kubernetes", fixture.Scope, 10);

        Assert.Contains(results, r => r.Record.Id == "rec-kube");
    }

    [Fact]
    public async Task Cluster_summary_record_joins_results()
    {
        using var fixture = await RetrieverFixture.CreateAsync();
        var retriever = fixture.CreateRetriever(new GraphOptions(Enabled: true, MaxNeighbors: 8, MaxHops: 2));

        var results = await retriever.RetrieveAsync("carol", fixture.Scope, 10);

        Assert.Contains(results, r => r.Record.Id == "summary-cluster-1");
    }

    [Fact]
    public async Task Unknown_query_yields_no_results()
    {
        using var fixture = await RetrieverFixture.CreateAsync();
        var retriever = fixture.CreateRetriever(new GraphOptions(Enabled: true, MaxNeighbors: 8, MaxHops: 2));

        var results = await retriever.RetrieveAsync("qqqqnope", fixture.Scope, 5);

        Assert.Empty(results);
    }

    [Fact]
    public async Task TopK_limits_result_count_and_ordering_is_score_descending()
    {
        using var fixture = await RetrieverFixture.CreateAsync();
        var retriever = fixture.CreateRetriever(new GraphOptions(Enabled: true, MaxNeighbors: 8, MaxHops: 2));

        var results = await retriever.RetrieveAsync("alice bob", fixture.Scope, 2);

        Assert.Equal(2, results.Count);
        Assert.True(results[0].Score >= results[1].Score);
    }

    [Fact]
    public async Task Scope_pinned_stores_keep_queries_isolated()
    {
        using var fixture = await RetrieverFixture.CreateAsync();
        var otherScope = new MemoryScope(MemoryScopeKind.Session, "other-scope");
        var retriever = fixture.CreateRetriever(new GraphOptions(Enabled: true, MaxNeighbors: 8, MaxHops: 2));

        var results = await retriever.RetrieveAsync("alice", otherScope, 5);

        // The other scope's pinned store and record provider see nothing structurally.
        Assert.Empty(results);
    }

    private sealed class RetrieverFixture : IDisposable
    {
        public string RootPath { get; }

        public MemoryScope Scope { get; } = new(MemoryScopeKind.Session, ScopeId);

        public JsonlMemoryGraphStore Store { get; private set; } = null!;

        public System.Collections.Generic.List<MemoryRecord> StoreRecords { get; } = new();

        private RetrieverFixture()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "pvmx-graph-retriever-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public static async Task<RetrieverFixture> CreateAsync()
        {
            var fixture = new RetrieverFixture();
            fixture.Store = new JsonlMemoryGraphStore(new MemoryRootLayout(fixture.RootPath, "model-hash-v1"), fixture.Scope);

            var records = new[]
            {
                Record("rec-alice-1", "Alice Smith plans the launch.", tags: new[] { "planning" }),
                Record("rec-bob-1", "Bob Jones reviews the budget."),
                Record("rec-carol-1", "Carol White ships the release."),
                Record("rec-dave-1", "Dave Miller tests the build."),
                Record("rec-dave-2", "Dave Miller signs off."),
                Record("rec-dave-3", "Dave Miller again."),
                Record("rec-kube", "cluster tooling notes", tags: new[] { "kubernetes" }),
                Record("summary-cluster-1", "Summary: release crew cluster."),
            };

            foreach (var record in records)
            {
                fixture.StoreRecords.Add(record);
            }

            // Entities with mentions pointing at their records.
            await fixture.Store.UpsertEntityAsync(new GraphEntity("alice-smith", "Alice Smith", new[] { "alice" }, new[] { "rec-alice-1" }));
            await fixture.Store.UpsertEntityAsync(new GraphEntity("bob-jones", "Bob Jones", new[] { "bob" }, new[] { "rec-bob-1" }));
            await fixture.Store.UpsertEntityAsync(new GraphEntity("carol-white", "Carol White", new[] { "carol" }, new[] { "rec-carol-1" }));
            await fixture.Store.UpsertEntityAsync(new GraphEntity("dave-miller", "Dave Miller", new[] { "dave" }, new[] { "rec-dave-1", "rec-dave-2", "rec-dave-3" }));

            // Relations: alice—bob (1 shared record), carol—dave (3 shared), ernest—carol (1).
            await fixture.Store.UpsertRelationAsync(new GraphRelation("alice-smith+bob-jones", "alice-smith", "bob-jones", "co-occurs", new[] { "rec-alice-1" }));
            await fixture.Store.UpsertRelationAsync(new GraphRelation("carol-white+dave-miller", "carol-white", "dave-miller", "co-occurs", new[] { "rec-dave-1", "rec-dave-2", "rec-dave-3" }));
            await fixture.Store.UpsertRelationAsync(new GraphRelation("ernest-blue+carol-white", "ernest-blue", "carol-white", "co-occurs", new[] { "rec-carol-1" }));

            // Cluster over carol+ernest with a summary record.
            await WriteClusterAsync(fixture.Store, new GraphCluster("cluster-1", new[] { "carol-white", "ernest-blue" }, "summary-cluster-1"));
            return fixture;
        }

        public GraphRetriever CreateRetriever(GraphOptions options)
        {
            var storeFactory = new Func<MemoryScope, IMemoryGraphStore>(scope =>
                scope.Id == ScopeId
                    ? Store
                    : new JsonlMemoryGraphStore(new MemoryRootLayout(RootPath, "model-hash-v1"), scope));
            var recordsProvider = new Func<MemoryScope, CancellationToken, Task<IReadOnlyList<MemoryRecord>>>((scope, token) =>
                Task.FromResult<IReadOnlyList<MemoryRecord>>(
                    scope.Id == ScopeId ? StoreRecords.ToArray() : Array.Empty<MemoryRecord>()));
            return new GraphRetriever(storeFactory, recordsProvider, options);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
            catch (IOException)
            {
                // best effort cleanup
            }
        }
    }

    private static async Task WriteClusterAsync(JsonlMemoryGraphStore store, GraphCluster cluster)
    {
        await store.UpsertClusterAsync(cluster);
    }

    private static MemoryRecord Record(string id, string content, string[]? tags = null)
    {
        return new MemoryRecord(
            Id: id,
            Scope: new MemoryScope(MemoryScopeKind.Session, ScopeId),
            Key: id,
            Content: content,
            Tags: tags);
    }
}