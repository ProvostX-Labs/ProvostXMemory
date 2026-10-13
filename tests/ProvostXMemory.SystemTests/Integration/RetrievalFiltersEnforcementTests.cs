using System;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory;
using ProvostXMemory.Abstractions;
using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;

using Xunit;

namespace ProvostXMemory.SystemTests.Integration;

/// <summary>
/// RetrievalFilters enforcement (wave-7 gap closure): tags/category/date filters carried
/// on RetrievalQuery are enforced post-fusion in the hybrid pipeline, identically across
/// the backend matrix (FlatExact, Ivf, Hnsw). Unfiltered recall is behaviorally unchanged.
/// </summary>
public sealed class RetrievalFiltersEnforcementTests
{
    private const int TopK = 8;

    public static TheoryData<string, IVectorIndexFactory, IndexKind> BackendMatrix() => new()
    {
        { "flat-exact", new EngineVectorIndexFactory(), IndexKind.Flat },
        { "ivf", SystemRig.IvfFactory(), IndexKind.Ivf },
        { "hnsw", SystemRig.HnswFactory(), IndexKind.Hnsw },
    };

    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static MemoryRecord Record(MemoryScope scope, string id, string content, string category, string[] tags, int dayOffset) =>
        new(id, scope, id, content, Category: category, Tags: tags)
        {
            CreatedAt = BaseTime.AddDays(dayOffset),
            UpdatedAt = BaseTime.AddDays(dayOffset),
        };

    /// <summary>Four-record corpus with distinct categories/tags/dates over one shared topic.</summary>
    private static (MemoryRecord[] Records, MemoryScope Scope) Corpus(MemoryScope scope) => (
        new[]
        {
            Record(scope, "alpha", "kubernetes rollout notes", "ops", new[] { "infra", "deploy" }, 0),
            Record(scope, "beta", "kubernetes rollout context", "ops", new[] { "deploy" }, 10),
            Record(scope, "gamma", "kubernetes rollout background", "docs", new[] { "infra" }, 20),
            Record(scope, "delta", "kubernetes rollout summary", "docs", new[] { "guide" }, 40),
        },
        scope);

    private static async Task<MemoryService> SeededAsync(string root, IVectorIndexFactory factory, IndexKind kind)
    {
        var service = SystemRig.BuildService(root, indexFactory: factory, indexKind: kind);
        var (records, scope) = Corpus(new MemoryScope(MemoryScopeKind.Purpose, "filters"));
        foreach (var record in records)
        {
            await service.RememberAsync(record);
        }

        await service.BuildIndexesAsync(new MemoryScope(MemoryScopeKind.Purpose, "filters"));
        return service;
    }

    [Theory]
    [MemberData(nameof(BackendMatrix))]
    public async Task Category_filter_returns_only_exact_category_across_backends(
        string backendName, IVectorIndexFactory factory, IndexKind kind)
    {
        _ = backendName;
        using var root = new TempRoot($"filters-{kind}");
        var service = await SeededAsync(root.Path_, factory, kind);
        var scope = new MemoryScope(MemoryScopeKind.Purpose, "filters");

        var filtered = await service.RecallAsync(new RetrievalQuery(
            "kubernetes rollout", scope, TopK, Filters: new RetrievalFilters(Category: "docs")));

        Assert.NotEmpty(filtered.Records);
        Assert.All(filtered.Records, hit => Assert.Equal("docs", hit.Record.Category));
        Assert.Contains(filtered.Records, hit => hit.Record.Id == "gamma");
        Assert.Contains(filtered.Records, hit => hit.Record.Id == "delta");
        Assert.DoesNotContain(filtered.Records, hit => hit.Record.Id == "alpha");
        Assert.DoesNotContain(filtered.Records, hit => hit.Record.Id == "beta");
    }

    [Theory]
    [MemberData(nameof(BackendMatrix))]
    public async Task Tag_filter_returns_only_any_match_across_backends(
        string backendName, IVectorIndexFactory factory, IndexKind kind)
    {
        _ = backendName;
        using var root = new TempRoot($"filters-{kind}");
        var service = await SeededAsync(root.Path_, factory, kind);
        var scope = new MemoryScope(MemoryScopeKind.Purpose, "filters");

        var filtered = await service.RecallAsync(new RetrievalQuery(
            "kubernetes rollout", scope, TopK, Filters: new RetrievalFilters(Tags: new[] { "deploy" })));

        Assert.NotEmpty(filtered.Records);
        Assert.All(filtered.Records, hit => Assert.Contains("deploy", hit.Record.Tags));
        Assert.Contains(filtered.Records, hit => hit.Record.Id == "alpha");
        Assert.Contains(filtered.Records, hit => hit.Record.Id == "beta");
        Assert.DoesNotContain(filtered.Records, hit => hit.Record.Id == "gamma");
        Assert.DoesNotContain(filtered.Records, hit => hit.Record.Id == "delta");
    }

    [Theory]
    [MemberData(nameof(BackendMatrix))]
    public async Task Date_from_filter_excludes_older_records_across_backends(
        string backendName, IVectorIndexFactory factory, IndexKind kind)
    {
        _ = backendName;
        using var root = new TempRoot($"filters-{kind}");
        var service = await SeededAsync(root.Path_, factory, kind);
        var scope = new MemoryScope(MemoryScopeKind.Purpose, "filters");

        var filtered = await service.RecallAsync(new RetrievalQuery(
            "kubernetes rollout", scope, TopK, Filters: new RetrievalFilters(From: BaseTime.AddDays(15))));

        Assert.NotEmpty(filtered.Records);
        Assert.All(filtered.Records, hit => Assert.True(hit.Record.CreatedAt >= BaseTime.AddDays(15)));
        Assert.Contains(filtered.Records, hit => hit.Record.Id == "delta");
        Assert.DoesNotContain(filtered.Records, hit => hit.Record.Id == "alpha");
    }

    [Theory]
    [MemberData(nameof(BackendMatrix))]
    public async Task Filters_that_match_nothing_return_empty_result_across_backends(
        string backendName, IVectorIndexFactory factory, IndexKind kind)
    {
        _ = backendName;
        using var root = new TempRoot($"filters-{kind}");
        var service = await SeededAsync(root.Path_, factory, kind);
        var scope = new MemoryScope(MemoryScopeKind.Purpose, "filters");

        var byCategory = await service.RecallAsync(new RetrievalQuery(
            "kubernetes rollout", scope, TopK, Filters: new RetrievalFilters(Category: "no-such-category")));
        var byTags = await service.RecallAsync(new RetrievalQuery(
            "kubernetes rollout", scope, TopK, Filters: new RetrievalFilters(Tags: new[] { "no-such-tag" })));
        var byDate = await service.RecallAsync(new RetrievalQuery(
            "kubernetes rollout", scope, TopK, Filters: new RetrievalFilters(From: BaseTime.AddDays(10_000))));

        Assert.Empty(byCategory.Records);
        Assert.Empty(byTags.Records);
        Assert.Empty(byDate.Records);
    }

    [Theory]
    [MemberData(nameof(BackendMatrix))]
    public async Task Unfiltered_recall_is_unchanged_by_filter_support(
        string backendName, IVectorIndexFactory factory, IndexKind kind)
    {
        _ = backendName;
        using var root = new TempRoot($"filters-{kind}");
        var service = await SeededAsync(root.Path_, factory, kind);
        var scope = new MemoryScope(MemoryScopeKind.Purpose, "filters");

        var plain = await service.RecallAsync(new RetrievalQuery("kubernetes rollout", scope, TopK));
        var withNullFilters = await service.RecallAsync(new RetrievalQuery(
            "kubernetes rollout", scope, TopK, Filters: null));

        Assert.True(plain.Records.Count >= 3);
        Assert.Equal(
            plain.Records.Select(hit => hit.Record.Id).ToArray(),
            withNullFilters.Records.Select(hit => hit.Record.Id).ToArray());
    }
}
