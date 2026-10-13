using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Eval;
using ProvostXMemory.Fusion;
using ProvostXMemory.Lexical;
using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;

using Xunit;

namespace ProvostXMemory.SystemTests.Unit;

/// <summary>
/// FusionOptions weight semantics on the fuser used by the facade: RRF rank-1 baselines
/// under default and extreme weights, both-weights-zero, huge-k flattening, and the
/// unknown-source fallback. Deterministic end-to-end behavior of the composition's
/// fusion stage.
/// </summary>
public sealed class FusionWeightTests
{
    private const double Precision = 1e-9;

    private static RankedList Dense(params string[] ids) =>
        new("dense", ids.Select(id => new ScoredId("Session", "s", id, 1.0)).ToList());

    private static RankedList Lexical(params string[] ids) =>
        new("lexical", ids.Select(id => new ScoredId("Session", "s", id, 1.0)).ToList());

    [Fact]
    public void Overlapping_item_wins_under_default_weights()
    {
        var fused = new ReciprocalRankFuser().Fuse(
            new[] { Dense("a", "b"), Lexical("b", "c") },
            new FusionOptions());

        // a = 1/61; b = 1/61 + 1/62 (two rank-1/2 appearances) → b leads.
        Assert.Equal("b", fused[0].Id);
        Assert.Equal(1.0 / 61.0 + 1.0 / 62.0, fused[0].Score, Precision);
    }

    [Fact]
    public void Zero_dense_weight_leaves_only_lexical_scores_positive()
    {
        var fused = new ReciprocalRankFuser().Fuse(
            new[] { Dense("a"), Lexical("x", "a") },
            new FusionOptions(DenseWeight: 0.0, LexicalWeight: 1.0));

        // x = 1/61 (lexical rank 1); a = 0 + 1/62 → x first, a present but halved.
        Assert.Equal("x", fused[0].Id);
        Assert.Equal(1.0 / 61.0, fused[0].Score, Precision);
        Assert.Contains(fused, hit => hit.Id == "a");
        Assert.Equal(1.0 / 62.0, fused.Single(hit => hit.Id == "a").Score, Precision);
    }

    [Fact]
    public void Zero_lexical_weight_leaves_only_dense_scores_positive()
    {
        var fused = new ReciprocalRankFuser().Fuse(
            new[] { Dense("a", "b"), Lexical("b") },
            new FusionOptions(DenseWeight: 1.0, LexicalWeight: 0.0));

        Assert.Equal("a", fused[0].Id);
        Assert.Equal("b", fused[1].Id);
        Assert.Equal(1.0 / 61.0, fused[0].Score, Precision);
        Assert.Equal(1.0 / 62.0, fused[1].Score, Precision); // lexical contribution suppressed
    }

    [Fact]
    public void Both_weights_zero_silence_every_listed_source_but_keep_unknowns()
    {
        var fused = new ReciprocalRankFuser().Fuse(
            new[]
            {
                Dense("a"),
                Lexical("b"),
                new RankedList("custom-stage", new[] { new ScoredId("Session", "s", "u", 1.0) }),
            },
            new FusionOptions(DenseWeight: 0.0, LexicalWeight: 0.0));

        Assert.Equal("u", fused[0].Id);
        Assert.Equal(1.0 / 61.0, fused[0].Score, Precision);
        Assert.All(fused.Where(hit => hit.Id is "a" or "b"), hit => Assert.Equal(0.0, hit.Score, Precision));
    }

    [Fact]
    public void Huge_rrf_k_flattens_all_contributions_toward_zero()
    {
        var fused = new ReciprocalRankFuser().Fuse(
            new[] { Dense("a"), Lexical("b") },
            new FusionOptions(RrfK: 1e9));

        Assert.Equal(2, fused.Count);
        // k + 1 ≈ k + 2 for k=1e9 → the two are nearly indistinguishable but ordering stays deterministic.
        Assert.Equal("a", fused[0].Id); // id tie-break (ordinal: a < b)
        Assert.Equal("b", fused[1].Id);
        Assert.True(fused[0].Score > 0.0);
        Assert.True(fused[0].Score < 1e-7);
    }

    [Fact]
    public void Tiny_rrf_k_amplifies_rank_differences()
    {
        var fused = new ReciprocalRankFuser().Fuse(
            new[] { Dense("a", "b") },
            new FusionOptions(RrfK: 1.0));

        // Rank 1 → 1/2, rank 2 → 1/3; amplification visible as a large gap.
        Assert.Equal(1.0 / 2.0, fused[0].Score, Precision);
        Assert.Equal(1.0 / 3.0, fused[1].Score, Precision);
        Assert.True(fused[0].Score - fused[1].Score > 0.1);
    }

    [Fact]
    public void Weighted_dominance_reorders_rank_one_hits()
    {
        // Lexical weight 5 pushes its rank-1 above both dense entries.
        var fused = new ReciprocalRankFuser().Fuse(
            new[] { Dense("a", "b"), Lexical("z") },
            new FusionOptions(DenseWeight: 1.0, LexicalWeight: 5.0));

        Assert.Equal("z", fused[0].Id);
        Assert.Equal(5.0 / 61.0, fused[0].Score, Precision);
        Assert.Equal("a", fused[1].Id);
        Assert.Equal("b", fused[2].Id);
    }

    [Fact]
    public void Empty_lists_are_tolerated_and_empty_input_yields_empty_output()
    {
        var fuser = new ReciprocalRankFuser();

        Assert.Empty(fuser.Fuse(Array.Empty<RankedList>(), new FusionOptions()));
        Assert.Empty(fuser.Fuse(new[] { new RankedList("dense", Array.Empty<ScoredId>()) }, new FusionOptions()));
    }

    [Fact]
    public void Null_arguments_throw()
    {
        var fuser = new ReciprocalRankFuser();

        Assert.Throws<ArgumentNullException>(() => fuser.Fuse(null!, new FusionOptions()));
        Assert.Throws<ArgumentNullException>(() => fuser.Fuse(new[] { Dense("a") }, null!));
    }
}

/// <summary>
/// HnswOptions determinism across two identical builds: same seed, same insertion order
/// and same queries must produce identical graph topology and identical ranked results,
/// and two different seeds may (and here do) differ — the seed actually drives level
/// assignment. Uses the real own-engine HNSW over the deterministic embedder.
/// </summary>
public sealed class HnswDeterminismTests
{
    private const int Dim = 128;

    private static async Task<List<VectorItem>> ItemsAsync(ClusterHashEmbedder embedder)
    {
        var texts = TopicCorpus.Documents.Select(document => document.Content).ToList();
        var items = new List<VectorItem>(texts.Count);
        for (var i = 0; i < texts.Count; i++)
        {
            items.Add(new VectorItem($"doc-{i}", await embedder.EmbedAsync(texts[i])));
        }
        return items;
    }

    [Fact]
    public async Task Same_seed_builds_identical_graphs_and_identical_results()
    {
        var embedder = new ClusterHashEmbedder(Dim);
        var items = await ItemsAsync(embedder);
        var options = new HnswOptions(M: 8, MaxLevel0Connections: 16, LevelFactor: 0.65, EfConstruction: 120, EfSearch: 96, Seed: 2026);

        var first = new HnswIndex(new VectorIndexGuard("agg-test", Dim), options);
        var second = new HnswIndex(new VectorIndexGuard("agg-test", Dim), options);
        await first.BuildAsync(items);
        await second.BuildAsync(items);

        Assert.Equal(first.GraphStats, second.GraphStats);
        Assert.Equal(first.Count, second.Count);

        // Deterministic corpus: a doc vector self-query is a golden probe. Every doc's
        // top-1 must be itself for both indexes, and the full hit lists must match exactly.
        for (var i = 0; i < TopicCorpus.Documents.Length; i++)
        {
            var (_, content) = TopicCorpus.Documents[i];
            var selfId = $"doc-{i}";
            var queryVector = await embedder.EmbedAsync(content);
            var hitsA = await first.SearchAsync(queryVector, 5);
            var hitsB = await second.SearchAsync(queryVector, 5);

            Assert.Equal(hitsA.Select(hit => hit.Id), hitsB.Select(hit => hit.Id));
            Assert.Equal(
                hitsA.Select(hit => hit.Score).Select(score => (double)score),
                hitsB.Select(hit => hit.Score).Select(score => (double)score));
            Assert.Equal(selfId, hitsA[0].Id);
            Assert.True(hitsA[0].Score > 0.999);
        }
    }

    [Fact]
    public async Task Same_seed_repeated_build_third_time_stays_identical()
    {
        var embedder = new ClusterHashEmbedder(Dim);
        var items = await ItemsAsync(embedder);
        var options = HnswOptions.Default;

        var reference = new HnswIndex(new VectorIndexGuard("agg-test", Dim), options);
        await reference.BuildAsync(items);
        var again = new HnswIndex(new VectorIndexGuard("agg-test", Dim), options);
        await again.BuildAsync(items);

        Assert.Equal(reference.GraphStats, again.GraphStats);
        var queryVector = await embedder.EmbedAsync(TopicCorpus.Documents[0].Content);
        Assert.Equal(
            (await reference.SearchAsync(queryVector, 4)).Select(hit => hit.Id),
            (await again.SearchAsync(queryVector, 4)).Select(hit => hit.Id));
        Assert.Equal("doc-0", (await reference.SearchAsync(queryVector, 4))[0].Id);
    }

    [Fact]
    public async Task Results_are_relevant_self_and_cluster_neighbors_not_noise()
    {
        var embedder = new ClusterHashEmbedder(Dim);
        var items = await ItemsAsync(embedder);
        var index = new HnswIndex(new VectorIndexGuard("agg-test", Dim), HnswOptions.Default);
        await index.BuildAsync(items);

        // kubernetes query → the three kube docs (doc-0..2) are the cluster members in
        // the deterministic corpus and must beat all foreign-cluster docs.
        var queryVector = await embedder.EmbedAsync("kubernetes helm rollout");
        var hits = (await index.SearchAsync(queryVector, 12)).Select(hit => hit.Id).ToList();

        var kubePositions = hits
            .Select((id, position) => (id, position))
            .Where(pair => pair.id is "doc-0" or "doc-1" or "doc-2")
            .Select(pair => pair.position)
            .ToList();
        Assert.Equal(3, kubePositions.Count);
        Assert.True(kubePositions.Max() < 6, $"kube docs should rank near the top, got positions {string.Join(',', kubePositions)} of {hits.Count}");
    }
}