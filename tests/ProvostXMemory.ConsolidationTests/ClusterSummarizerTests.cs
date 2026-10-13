using ProvostXMemory.Abstractions;
using ProvostXMemory.Consolidation;

using Xunit;

namespace ProvostXMemory.ConsolidationTests;

/// <summary>ClusterSummarizer: deterministic summary shape, meta flags, cluster id stability.</summary>
public sealed class ClusterSummarizerTests
{
    private static ClusterSummarizer CreateSummarizer() => new();

    private static List<MemoryRecord> Cluster()
    {
        return new List<MemoryRecord>
        {
            TestRecords.Create("b2", "login flow", "User cannot log in when SSO certificate expires.", tags: new[] { "auth" }),
            TestRecords.Create("a1", "login flow", "Login breaks after SSO certificate rotation.", tags: new[] { "auth", "sso" }),
            TestRecords.Create("c3", "login flow", "SSO certificate expiry blocks login page.", tags: new[] { "infra" }),
        };
    }

    [Fact]
    public async Task Summary_has_category_summary_and_flag_meta()
    {
        var summarizer = CreateSummarizer();
        var summary = await summarizer.SummarizeAsync("Session", "s1", Cluster());

        Assert.Equal("summary", summary.Category);
        Assert.Equal("true", summary.Meta["cluster-summary"]);
        Assert.Equal("3", summary.Meta["cluster-member-count"]);
        Assert.StartsWith("summary-", summary.Id);
        Assert.Equal(new[] { "a1", "b2", "c3" }, summary.Meta["cluster-member-ids"].Split(','));
        Assert.Equal("Session", summary.Scope.Kind.ToString());
        Assert.Equal("s1", summary.Scope.Id);
    }

    [Fact]
    public async Task Summary_content_contains_keys_and_excerpts()
    {
        var summarizer = CreateSummarizer();
        var summary = await summarizer.SummarizeAsync("Session", "s1", Cluster());

        // The representative excerpts reference member ids + keys; content is the
        // concatenated keys + " - id | key: excerpt" lines. Keys are identical here → one.
        Assert.Contains("login flow", summary.Key);
        Assert.Contains("[cluster login flow]", summary.Content);
        Assert.Contains("a1 | login flow:", summary.Content);
        Assert.Contains("b2 | login flow:", summary.Content);
        Assert.Contains("c3 | login flow:", summary.Content);
    }

    [Fact]
    public async Task Summary_id_is_stable_regardless_of_cluster_order()
    {
        var summarizer = CreateSummarizer();
        var cluster = Cluster();
        var forward = await summarizer.SummarizeAsync("Session", "s1", cluster);
        var reversed = await summarizer.SummarizeAsync("Session", "s1", cluster.AsEnumerable().Reverse().ToList());

        Assert.Equal(forward.Id, reversed.Id);
        Assert.Equal(forward.Meta["cluster-id"], reversed.Meta["cluster-id"]);
        Assert.Equal(forward.Content, reversed.Content);
    }

    [Fact]
    public async Task Summary_tags_union_input_tags_deterministically()
    {
        var summarizer = CreateSummarizer();
        var summary = await summarizer.SummarizeAsync("Session", "s1", Cluster());

        Assert.Equal(new[] { "auth", "infra", "sso" }, summary.Tags);
    }

    [Fact]
    public async Task Excerpt_limit_caps_listed_members()
    {
        var summarizer = new ClusterSummarizer(new ConsolidationTuning { SummaryExcerptLimit = 2 });
        var largeCluster = Enumerable.Range(0, 5)
            .Select(i => TestRecords.Create($"id-{i}", $"key-{i}", $"content {i} body"))
            .ToList();

        var summary = await summarizer.SummarizeAsync("Session", "s1", largeCluster);

        Assert.Equal("5", summary.Meta["cluster-member-count"]);
        Assert.Contains("key-0:", summary.Content);
        Assert.Contains("key-1:", summary.Content);
        Assert.DoesNotContain("key-2:", summary.Content);
    }

    [Fact]
    public async Task Excerpt_length_is_truncated()
    {
        var summarizer = new ClusterSummarizer(new ConsolidationTuning { SummaryExcerptLength = 10 });
        var cluster = new List<MemoryRecord>
        {
            TestRecords.Create("only", "the-key", "this content is far longer than ten characters"),
        };

        var summary = await summarizer.SummarizeAsync("Session", "s1", cluster);

        Assert.Contains("this conte…", summary.Content);
    }

    [Fact]
    public async Task Empty_cluster_is_rejected()
    {
        var summarizer = CreateSummarizer();
        await Assert.ThrowsAsync<ArgumentException>(
            () => summarizer.SummarizeAsync("Session", "s1", new List<MemoryRecord>()));
    }

    [Fact]
    public async Task Scope_of_summary_matches_requested_scope()
    {
        var summarizer = CreateSummarizer();
        var summary = await summarizer.SummarizeAsync("Tenant", "acme", Cluster());
        Assert.Equal("Tenant", summary.Scope.Kind.ToString());
        Assert.Equal("acme", summary.Scope.Id);
    }
}