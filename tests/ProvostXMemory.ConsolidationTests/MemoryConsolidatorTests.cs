using ProvostXMemory.Abstractions;
using ProvostXMemory.Consolidation;

using Xunit;

namespace ProvostXMemory.ConsolidationTests;

/// <summary>MemoryConsolidator end-to-end over in-memory store + fake embedder.</summary>
public sealed class MemoryConsolidatorTests
{
    private const string ScopeKind = "Session";
    private const string ScopeId = "s1";

    private static (MemoryConsolidator consolidator, InMemoryMemoryStore store) Create()
    {
        var store = new InMemoryMemoryStore();
        var embedder = new HashFakeEmbedder();
        var consolidator = new MemoryConsolidator(
            store,
            new ThresholdDedupDetector(embedder),
            embedder,
            new ClusterSummarizer());
        return (consolidator, store);
    }

    private static async Task SeedAsync(InMemoryMemoryStore store, params MemoryRecord[] records)
    {
        foreach (var record in records)
        {
            await store.AddAsync(record);
        }
    }

    [Fact]
    public async Task Empty_scope_is_no_op()
    {
        var (consolidator, store) = Create();

        var result = await consolidator.ConsolidateAsync(ScopeKind, ScopeId, new ConsolidationOptions());

        Assert.Empty(result);
        Assert.Equal(0, await store.CountAsync(ScopeKind, ScopeId));
    }

    [Fact]
    public async Task Expired_ttl_records_are_deleted()
    {
        var (consolidator, store) = Create();
        var past = DateTimeOffset.UtcNow.AddHours(-1);
        var fresh = DateTimeOffset.UtcNow.AddHours(1);
        await SeedAsync(
            store,
            TestRecords.Create("gone", "k1", "alpha bravo", expiresAt: past),
            TestRecords.Create("stays", "k2", "xray yankee", expiresAt: fresh),
            TestRecords.Create("eternal", "k3", "zulu uniform"));

        var result = await consolidator.ConsolidateAsync(ScopeKind, ScopeId, new ConsolidationOptions());

        Assert.Null(await store.GetAsync(ScopeKind, ScopeId, "gone"));
        Assert.NotNull(await store.GetAsync(ScopeKind, ScopeId, "stays"));
        Assert.NotNull(await store.GetAsync(ScopeKind, ScopeId, "eternal"));
        Assert.Equal(new[] { "eternal", "stays" }, result.Select(record => record.Id));
        Assert.Equal(2, await store.CountAsync(ScopeKind, ScopeId));
    }

    [Fact]
    public async Task Ttl_expiry_can_be_disabled_via_options()
    {
        var (consolidator, store) = Create();
        await SeedAsync(
            store,
            TestRecords.Create("gone", "k1", "alpha bravo", expiresAt: DateTimeOffset.UtcNow.AddHours(-1)));

        await consolidator.ConsolidateAsync(ScopeKind, ScopeId, new ConsolidationOptions(ExpireTtl: false));

        Assert.NotNull(await store.GetAsync(ScopeKind, ScopeId, "gone"));
    }

    [Fact]
    public async Task Duplicates_are_removed_and_tags_unioned_into_oldest()
    {
        var (consolidator, store) = Create();
        var now = DateTimeOffset.UtcNow;
        var olderKept = TestRecords.Create("keep-me", "login", "alpha bravo charlie delta", tags: new[] { "auth" })
            with { CreatedAt = now.AddMinutes(-10) };
        var newerDupe = TestRecords.Create("drop-me", "login", "alpha bravo charlie delta", tags: new[] { "auth", "login-page" })
            with { CreatedAt = now };
        var unrelated = TestRecords.Create("unrelated", "other", "the quick brown fox jumps over", tags: new[] { "misc" })
            with { CreatedAt = now.AddMinutes(-5) };
        await SeedAsync(store, newerDupe, olderKept, unrelated);

        var result = await consolidator.ConsolidateAsync(ScopeKind, ScopeId, new ConsolidationOptions());

        // Oldest member of the duplicate group survives with unioned tags.
        Assert.Null(await store.GetAsync(ScopeKind, ScopeId, "drop-me"));
        var kept = await store.GetAsync(ScopeKind, ScopeId, "keep-me");
        Assert.NotNull(kept);
        Assert.Equal(new[] { "auth", "login-page" }, kept!.Tags);
        // Unaffected records stay.
        Assert.NotNull(await store.GetAsync(ScopeKind, ScopeId, "unrelated"));
        // Returned list = survivors (ordered by id).
        Assert.Equal(new[] { "keep-me", "unrelated" }, result.Select(record => record.Id));
    }

    [Fact]
    public async Task Duplicate_group_keeps_exactly_the_oldest_member()
    {
        var (consolidator, store) = Create();
        var now = DateTimeOffset.UtcNow;
        await SeedAsync(
            store,
            TestRecords.Create("d2", "k", "alpha bravo charlie delta", tags: new[] { "two" }) with { CreatedAt = now.AddMinutes(-2) },
            TestRecords.Create("d1", "k", "alpha bravo charlie delta", tags: new[] { "one" }) with { CreatedAt = now.AddMinutes(-1) },
            TestRecords.Create("d3", "k", "alpha bravo charlie delta", tags: new[] { "three" }) with { CreatedAt = now.AddMinutes(-3) });

        await consolidator.ConsolidateAsync(ScopeKind, ScopeId, new ConsolidationOptions());

        // d3 is the oldest (created 3 minutes ago) — d1 and d2 are deleted.
        Assert.NotNull(await store.GetAsync(ScopeKind, ScopeId, "d3"));
        Assert.Null(await store.GetAsync(ScopeKind, ScopeId, "d1"));
        Assert.Null(await store.GetAsync(ScopeKind, ScopeId, "d2"));
        var kept = await store.GetAsync(ScopeKind, ScopeId, "d3");
        Assert.Equal(new[] { "one", "three", "two" }, kept!.Tags);
    }

    [Fact]
    public async Task Cluster_summary_is_emitted_when_enabled_and_cluster_is_large_enough()
    {
        var (consolidator, store) = Create();
        await SeedAsync(
            store,
            TestRecords.Create("c1", "login", "user cannot login certificate"),
            TestRecords.Create("c2", "login", "user cannot login certificate again"),
            TestRecords.Create("c3", "login", "user cannot login certificate today"),
            TestRecords.Create("far", "weather", "completely unrelated weather report text"));

        var result = await consolidator.ConsolidateAsync(
            ScopeKind, ScopeId, new ConsolidationOptions(EmitClusterSummaries: true));

        // Sources are kept; a summary record is appended to store and result.
        Assert.NotNull(await store.GetAsync(ScopeKind, ScopeId, "c1"));
        var storeIds = (await store.ListAsync(ScopeKind, ScopeId)).Select(record => record.Id).ToList();
        var summary = Assert.Single(result, record => record.Category == "summary");
        Assert.Contains(summary.Id, storeIds);
        Assert.Equal("true", summary.Meta["cluster-summary"]);
        // Result = survivors (id-ordered) then summaries.
        Assert.Equal(new[] { "c1", "c2", "c3", "far" }, result.Take(4).Select(record => record.Id));
    }

    [Fact]
    public async Task Small_clusters_emit_no_summary()
    {
        var (consolidator, store) = Create();
        await SeedAsync(
            store,
            TestRecords.Create("c1", "login", "user cannot login certificate"),
            TestRecords.Create("c2", "login", "user cannot login certificate again"),
            TestRecords.Create("far", "weather", "completely unrelated weather report text"));

        var result = await consolidator.ConsolidateAsync(
            ScopeKind, ScopeId, new ConsolidationOptions(EmitClusterSummaries: true));

        // Pair is below the default SummarizeMinClusterSize = 3 → no summary; nothing deleted.
        Assert.DoesNotContain(result, record => record.Category == "summary");
        Assert.Equal(3, await store.CountAsync(ScopeKind, ScopeId));
    }

    [Fact]
    public async Task Summaries_are_disabled_by_default()
    {
        var (consolidator, store) = Create();
        await SeedAsync(
            store,
            TestRecords.Create("c1", "login", "user cannot login certificate"),
            TestRecords.Create("c2", "login", "user cannot login certificate again"),
            TestRecords.Create("c3", "login", "user cannot login certificate today"));

        var result = await consolidator.ConsolidateAsync(ScopeKind, ScopeId, new ConsolidationOptions());

        Assert.DoesNotContain(result, record => record.Category == "summary");
        Assert.Equal(3, await store.CountAsync(ScopeKind, ScopeId));
    }

    [Fact]
    public async Task Scope_parameters_are_validated()
    {
        var (consolidator, _) = Create();
        await Assert.ThrowsAsync<ArgumentException>(
            () => consolidator.ConsolidateAsync("", ScopeId, new ConsolidationOptions()));
        await Assert.ThrowsAsync<ArgumentException>(
            () => consolidator.ConsolidateAsync(ScopeKind, " ", new ConsolidationOptions()));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => consolidator.ConsolidateAsync(ScopeKind, ScopeId, null!));
    }

    [Fact]
    public async Task Consolidation_is_idempotent_when_rerun()
    {
        var (consolidator, store) = Create();
        await SeedAsync(
            store,
            TestRecords.Create("keep-me", "login", "alpha bravo charlie delta", tags: new[] { "auth" }),
            TestRecords.Create("drop-me", "login", "alpha bravo charlie delta", tags: new[] { "login-page" }));

        await consolidator.ConsolidateAsync(ScopeKind, ScopeId, new ConsolidationOptions());
        var countAfterFirst = await store.CountAsync(ScopeKind, ScopeId);
        await consolidator.ConsolidateAsync(ScopeKind, ScopeId, new ConsolidationOptions());

        Assert.Equal(countAfterFirst, await store.CountAsync(ScopeKind, ScopeId));
        Assert.Equal(1, countAfterFirst);
    }
}