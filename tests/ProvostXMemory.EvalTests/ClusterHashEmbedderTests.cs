using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ProvostXMemory.Eval;

using Xunit;

namespace ProvostXMemory.EvalTests;

/// <summary>
/// Deterministic fake embedder: pure function of text (no randomness), cluster-preserving
/// geometry (same cluster → high cosine), foreign cluster → low cosine.
/// </summary>
public sealed class ClusterHashEmbedderTests
{
    private readonly ClusterHashEmbedder _embedder = new(dim: 128);

    [Fact]
    public async Task Same_text_same_vector_always()
    {
        var first = await _embedder.EmbedAsync("kubernetes cluster rollout helm");
        var second = await _embedder.EmbedAsync("kubernetes cluster rollout helm");

        Assert.Equal(first, second);
        Assert.Equal(128, first.Length);
    }

    [Fact]
    public async Task Same_cluster_similar_content_lands_near_each_other()
    {
        // Two different orderings of the same cluster terms + filler → high cosine.
        var a = await _embedder.EmbedAsync("kubernetes helm rollout cluster release chart notes summary");
        var b = await _embedder.EmbedAsync("release chart kubernetes cluster helm rollout summary notes");

        Assert.True(Cosine(a, b) > 0.85, $"same-cluster cosine {Cosine(a, b)}");
    }

    [Fact]
    public async Task Different_clusters_stay_far_apart()
    {
        var kubernetes = await _embedder.EmbedAsync("kubernetes helm rollout cluster release chart notes summary");
        var baking = await _embedder.EmbedAsync("sourdough banneton ferment crumb oven starter notes summary");

        Assert.True(Cosine(kubernetes, baking) < 0.3, $"cross-cluster cosine {Cosine(kubernetes, baking)}");
    }

    [Fact]
    public async Task Query_to_its_cluster_records_scores_higher_than_foreign()
    {
        var query = await _embedder.EmbedAsync("invoice billing stripe refund chargeback receipt");
        var own = await _embedder.EmbedAsync("stripe refund invoice chargeback billing receipt notes summary");
        var foreign = await _embedder.EmbedAsync("schengen residence permit visa embassy passport notes summary");

        Assert.True(Cosine(query, own) > Cosine(query, foreign) + 0.4);
    }

    [Fact]
    public async Task Empty_and_unknown_text_give_low_norm_but_valid_vectors()
    {
        var empty = await _embedder.EmbedAsync(string.Empty);
        var unknown = await _embedder.EmbedAsync("zzz qqq");

        Assert.All(empty, value => Assert.Equal(0f, value));
        Assert.Equal(128, unknown.Length);
        Assert.True(Norm(unknown) > 0f);
    }

    [Fact]
    public void Model_hash_reflects_dim()
    {
        var small = new ClusterHashEmbedder(dim: 64);
        Assert.Equal("cluster-hash-v1-d64", small.ModelHash);
        Assert.Equal(64, small.Dim);
    }

    [Fact]
    public void Invalid_construction_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClusterHashEmbedder(dim: 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClusterHashEmbedder(dim: 8192));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClusterHashEmbedder(noiseAmplitude: 0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClusterHashEmbedder(noiseAmplitude: -0.1f));
    }

    [Fact]
    public async Task Null_text_rejected()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await _embedder.EmbedAsync(null!));
    }

    // Pure cosine over float spans; intentionally static per architecture rule.
    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        return na <= 0 || nb <= 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    // Pure euclidean norm; intentionally static per architecture rule.
    private static double Norm(float[] vector)
    {
        double sum = 0;
        foreach (var value in vector)
        {
            sum += value * value;
        }

        return Math.Sqrt(sum);
    }
}