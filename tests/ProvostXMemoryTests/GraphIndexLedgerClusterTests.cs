using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Consolidation;
using ProvostXMemory.Graph;
using ProvostXMemory.Storage;

using Xunit;

namespace ProvostXMemoryTests;

/// <summary>
/// GraphIndexLedger cluster recording: IClusterSummarizer-shaped summaries are recorded as
/// graph clusters linking their member entities (scope-bound), GraphRetriever surfaces the
/// summary through cluster evidence, and non-summaries / vanished members record nothing.
/// </summary>
public sealed class GraphIndexLedgerClusterTests : IDisposable
{
    private const string ScopeId = "s1";

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "pvmx-ledger-cluster-tests", Guid.NewGuid().ToString("N"));

    private readonly MemoryRootLayout _layout;

    public GraphIndexLedgerClusterTests()
    {
        _layout = new MemoryRootLayout(_root, "model-hash-v1");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private MemoryScope Scope() => new(MemoryScopeKind.Session, ScopeId);

    private static JsonlMemoryGraphStore GraphStore(MemoryRootLayout layout, MemoryScope scope)
        => new(layout, scope);

    private async Task<(GraphIndexLedger Ledger, JsonlMemoryStore Store)> CreateLedgerAsync()
    {
        var store = new JsonlMemoryStore(_layout);
        var scope = Scope();
        await store.AddAsync(new MemoryRecord(
            "m1", scope, "m1", "Ada Lovelace wrote the spec."));
        await store.AddAsync(new MemoryRecord(
            "m2", scope, "m2", "Grace Hopper reviewed the spec."));
        var ledger = new GraphIndexLedger(
            new HeuristicEntityExtractor(),
            scopeArgument => GraphStore(_layout, scopeArgument),
            (scopeArgument, token) => store.SnapshotAsync(scopeArgument.Kind.ToString(), scopeArgument.Id, token));
        return (ledger, store);
    }

    /// <summary>A summary shaped exactly like ClusterSummarizer's output.</summary>
    private static MemoryRecord Summary(string clusterId, string memberIds)
    {
        return new MemoryRecord(
            Id: "summary-" + clusterId,
            Scope: new MemoryScope(MemoryScopeKind.Session, ScopeId),
            Key: "members",
            Content: "[cluster members]",
            Category: ClusterSummarizer.SummaryCategory)
        {
            Meta = new System.Collections.Generic.Dictionary<string, string>
            {
                ["cluster-summary"] = "true",
                ["cluster-id"] = clusterId,
                ["cluster-member-ids"] = memberIds,
            },
        };
    }

    [Fact]
    public async Task Cluster_summary_is_recorded_with_member_entities_and_summary_id()
    {
        var (ledger, _) = await CreateLedgerAsync();
        var summary = Summary("c1", "m1,m2");

        var cluster = await ledger.RecordClusterSummaryAsync(summary);

        Assert.NotNull(cluster);
        Assert.Equal("c1", cluster!.ClusterId);
        Assert.Equal("summary-c1", cluster.SummaryRecordId);
        Assert.Contains("ada-lovelace", cluster.EntityIds);
        Assert.Contains("grace-hopper", cluster.EntityIds);

        // The cluster persisted through the store API into the scope's clusters.jsonl.
        var store = GraphStore(_layout, Scope());
        var persisted = await store.ClusterOfAsync("ada-lovelace");
        Assert.NotNull(persisted);
        Assert.Equal("summary-c1", persisted!.SummaryRecordId);

        var lines = (await File.ReadAllLinesAsync(store.ClustersPath))
            .Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        Assert.Single(lines);
    }

    [Fact]
    public async Task Cluster_summary_is_surfaceable_through_graph_retriever()
    {
        var (ledger, _) = await CreateLedgerAsync();
        await ledger.RecordClusterSummaryAsync(Summary("c1", "m1,m2"));

        var store = GraphStore(_layout, Scope());
        var snapshots = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<MemoryRecord>>(StringComparer.Ordinal)
        {
            [$"{ScopeId}"] = new System.Collections.Generic.List<MemoryRecord>
            {
                new("m1", Scope(), "m1", "Ada Lovelace wrote the spec."),
                new("m2", Scope(), "m2", "Grace Hopper reviewed the spec."),
                new("summary-c1", Scope(), "members", "[cluster members]", ClusterSummarizer.SummaryCategory),
            },
        };
        var retriever = new GraphRetriever(
            scopeArgument => GraphStore(_layout, scopeArgument),
            (scopeArgument, token) => Task.FromResult(
                snapshots.TryGetValue(scopeArgument.Id, out var records)
                    ? records
                    : System.Array.Empty<MemoryRecord>()));

        var results = await retriever.RetrieveAsync("ada", Scope(), 5);

        // Entity evidence (member record) plus cluster evidence (summary record id).
        Assert.Contains(results, hit => hit.Record.Id == "m1");
        Assert.Contains(results, hit => hit.Record.Id == "summary-c1");
    }

    [Fact]
    public async Task Non_summaries_and_summaries_without_member_ids_record_nothing()
    {
        var (ledger, store) = await CreateLedgerAsync();

        // Not a summary (no meta flag).
        Assert.Null(await ledger.RecordClusterSummaryAsync(new MemoryRecord(
            "plain", Scope(), "plain", "Ada Lovelace content again.")));

        // Flagged but without a cluster id to link.
        var flaggedNoId = Summary("c2", "m1,m2");
        flaggedNoId = flaggedNoId with { Meta = new System.Collections.Generic.Dictionary<string, string> { ["cluster-summary"] = "true" } };
        Assert.Null(await ledger.RecordClusterSummaryAsync(flaggedNoId));

        // Flagged with an id but all members missing from the scope's snapshot.
        Assert.Null(await ledger.RecordClusterSummaryAsync(Summary("c3", "vanished-1,vanished-2")));

        // The scope's clusters file was never created — nothing recorded anywhere.
        Assert.False(File.Exists(GraphStore(_layout, Scope()).ClustersPath));
        Assert.Equal(2, await store.CountAsync("Session", ScopeId));
    }

    [Fact]
    public async Task Partial_member_overlap_still_records_surviving_members()
    {
        var (ledger, _) = await CreateLedgerAsync();

        // m1 exists, m3 vanished: a partial cluster is strictly better than a dropped one.
        var cluster = await ledger.RecordClusterSummaryAsync(Summary("c1", "m1,m3"));

        Assert.NotNull(cluster);
        Assert.Contains("ada-lovelace", cluster!.EntityIds);
        Assert.DoesNotContain("vanished", cluster.EntityIds);
        var store = GraphStore(_layout, Scope());
        Assert.NotNull(await store.ClusterOfAsync("ada-lovelace"));
    }
}