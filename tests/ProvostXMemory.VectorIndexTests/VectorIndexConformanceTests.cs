using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.VectorIndex;

using Xunit;

namespace ProvostXMemory.VectorIndexTests;

/// <summary>
/// Shared IVectorIndex conformance suite. Every backend (FlatJson, FlatExact, IvfFlat)
/// derives fixtures from this class. Exact backends must match exactly on build/add/
/// remove/search/dim-guard/empty behaviors; IVF overrides the exact-match search test
/// with a documented recall@10 ≥ 0.95 tolerance on the same fixtures.
/// </summary>
public abstract class VectorIndexConformanceTests
{
    private const int Dim = 8;

    /// <summary>Factory hook: a fresh, empty backend instance per test.</summary>
    protected abstract IVectorIndex CreateIndex();

    /// <summary>Whether the backend ranks exactly (false → subclass overrides search tests with recall tolerance).</summary>
    protected virtual bool ExactRanking => true;

    // ---------- fixtures ----------

    protected static VectorItem Item(string id, params float[] coords)
    {
        return new VectorItem(id, coords);
    }

    /// <summary>Deterministic fixture corpus: 40 vectors, structured angles + id-tagged offsets.</summary>
    protected static List<VectorItem> FixtureCorpus()
    {
        var items = new List<VectorItem>();
        for (var i = 0; i < 40; i++)
        {
            var vector = new float[Dim];
            for (var d = 0; d < Dim; d++)
            {
                vector[d] = MathF.Sin(((i + 1) * (d + 3)) * 0.37f) * (1f + (i % 5));
            }

            items.Add(new VectorItem($"id-{i:D2}", vector));
        }

        return items;
    }

    /// <summary>Deterministic query set aligned with the corpus (query i near id-{i:00}).</summary>
    protected static List<(string ExpectedId, float[] Query)> FixtureQueries()
    {
        var queries = new List<(string, float[])>();
        for (var i = 0; i < 40; i += 4)
        {
            var query = new float[Dim];
            for (var d = 0; d < Dim; d++)
            {
                query[d] = MathF.Sin(((i + 1) * (d + 3)) * 0.37f) * (1f + (i % 5));
            }

            for (var d = 0; d < Dim; d++)
            {
                query[d] += 0.01f * d;
            }

            queries.Add(($"id-{i:D2}", query));
        }

        return queries;
    }

    private static float[] Corrupt(float[] source, float amount)
    {
        var copy = (float[])source.Clone();
        for (var d = 0; d < copy.Length; d++)
        {
            copy[d] += amount * (d + 1);
        }

        return copy;
    }

    // ---------- build / add / remove / count semantics ----------

    [Fact]
    public async Task Build_sets_count_and_overwrites_duplicates()
    {
        var index = CreateIndex();
        Assert.Equal(0, index.Count);

        var corpus = FixtureCorpus();
        await index.BuildAsync(corpus);
        Assert.Equal(corpus.Count, index.Count);

        // rebuild with duplicates + one replacement must count once per id
        var dupes = new List<VectorItem>(corpus)
        {
            Item("id-00", 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
            Item("id-00", 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f),
        };
        await index.BuildAsync(dupes);
        Assert.Equal(corpus.Count, index.Count);
    }

    [Fact]
    public async Task Add_upserts_and_counts_once_per_id()
    {
        var index = CreateIndex();
        await index.BuildAsync(FixtureCorpus());
        var before = index.Count;

        await index.AddAsync(Item("new-1", 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f));
        Assert.Equal(before + 1, index.Count);

        await index.AddAsync(Item("new-1", 8f, 7f, 6f, 5f, 4f, 3f, 2f, 1f));
        Assert.Equal(before + 1, index.Count);

        await index.RemoveAsync("new-1");
        Assert.Equal(before, index.Count);
    }

    [Fact]
    public async Task Remove_unknown_id_throws()
    {
        var index = CreateIndex();
        await index.BuildAsync(FixtureCorpus());
        await Assert.ThrowsAsync<VectorIndexIdNotFoundException>(
            () => index.RemoveAsync("nope"));
    }

    [Fact]
    public async Task Remove_shifts_results_without_trace()
    {
        var index = CreateIndex();
        var corpus = FixtureCorpus();
        await index.BuildAsync(corpus);

        var query = corpus[7].Vector;
        var before = await index.SearchAsync(query, 3);
        Assert.Equal("id-07", before[0].Id);

        await index.RemoveAsync("id-07");
        var after = await index.SearchAsync(query, 3);
        Assert.DoesNotContain(after, hit => hit.Id == "id-07");
        Assert.Equal(before.Count, after.Count);
    }

    // ---------- search semantics ----------

    [Fact]
    public async Task Search_returns_k_results_sorted_descending()
    {
        var index = CreateIndex();
        var corpus = FixtureCorpus();
        await index.BuildAsync(corpus);

        var hits = await index.SearchAsync(corpus[0].Vector, 5);
        Assert.Equal(5, hits.Count);
        for (var i = 1; i < hits.Count; i++)
        {
            Assert.True(hits[i - 1].Score >= hits[i].Score, "results must be non-increasing in score");
        }

        Assert.Equal(5, hits.Select(static hit => hit.Id).Distinct().Count());
    }

    [Fact]
    public async Task Search_k_larger_than_count_returns_all()
    {
        var index = CreateIndex();
        await index.BuildAsync(FixtureCorpus());
        var hits = await index.SearchAsync(FixtureCorpus()[0].Vector, 1_000);
        Assert.Equal(40, hits.Count);
    }

    [Fact]
    public async Task Self_query_is_top_hit_when_present()
    {
        var index = CreateIndex();
        var corpus = FixtureCorpus();
        await index.BuildAsync(corpus);

        var hits = await index.SearchAsync(corpus[23].Vector, 1);
        Assert.Equal("id-23", hits[0].Id);
        Assert.True(hits[0].Score > 0.99f, $"self similarity should be ~1, was {hits[0].Score}");
    }

    [Fact]
    public async Task Exact_backends_rank_identically_on_fixture_queries()
    {
        if (!ExactRanking)
        {
            return; // IVF overrides with recall-based test
        }

        var index = CreateIndex();
        var corpus = FixtureCorpus();
        await index.BuildAsync(corpus);

        foreach (var (expectedId, query) in FixtureQueries())
        {
            var hits = await index.SearchAsync(query, 10);
            Assert.NotEmpty(hits);
            Assert.Equal(expectedId, hits[0].Id);
        }
    }

    // ---------- guards ----------

    [Fact]
    public async Task Search_dim_mismatch_throws()
    {
        var index = CreateIndex();
        await index.BuildAsync(FixtureCorpus());

        await Assert.ThrowsAsync<VectorIndexGuardMismatchException>(
            () => index.SearchAsync(new float[Dim + 1], 3));
        await Assert.ThrowsAsync<VectorIndexGuardMismatchException>(
            () => index.SearchAsync(new float[Dim - 1], 3));
    }

    [Fact]
    public async Task Add_dim_mismatch_throws()
    {
        var index = CreateIndex();
        await Assert.ThrowsAsync<VectorIndexGuardMismatchException>(
            () => index.AddAsync(Item("bad", 1f, 2f, 3f)));
        Assert.Equal(0, index.Count);
    }

    // ---------- empty index behavior ----------

    [Fact]
    public async Task Empty_index_searches_return_empty_and_flush_is_safe()
    {
        var index = CreateIndex();
        var hits = await index.SearchAsync(MakeQueryVector(), 3);
        Assert.Empty(hits);
        Assert.Equal(0, index.Count);

        await index.FlushAsync();
        await index.BuildAsync(Array.Empty<VectorItem>());
        Assert.Empty(await index.SearchAsync(MakeQueryVector(), 3));
    }

    [Fact]
    public async Task Negative_k_throws()
    {
        var index = CreateIndex();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => index.SearchAsync(MakeQueryVector(), -1));
    }

    protected static float[] MakeQueryVector()
    {
        var vector = new float[Dim];
        for (var d = 0; d < Dim; d++)
        {
            vector[d] = 0.125f * (d + 1);
        }

        return vector;
    }
}