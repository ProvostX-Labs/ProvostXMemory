using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;

using Xunit;

namespace ProvostXMemory.VectorIndexTests;

/// <summary>VectorMath kernels: scalar ground truth vs SIMD paths, normalization, cosine.</summary>
public sealed class VectorMathTests
{
    private static float[] Vec(int seed, int dim)
    {
        var vector = new float[dim];
        for (var d = 0; d < dim; d++)
        {
            vector[d] = MathF.Sin((seed + 1) * (d + 1) * 0.53f) * (1 + (seed % 3));
        }

        return vector;
    }

    [Fact]
    public void Dot_matches_scalar_ground_truth_across_lengths_and_widths()
    {
        foreach (var dim in new[] { 1, 3, 7, 8, 15, 16, 17, 31, 32, 64, 129 })
        {
            var a = Vec(dim, dim);
            var b = Vec(dim + 5, dim);
            var expected = 0f;
            for (var i = 0; i < dim; i++)
            {
                expected += a[i] * b[i];
            }

            var actual = VectorMath.Dot(a, b);
            var tolerance = MathF.Abs(expected) * 1e-5f + 1e-6f;
            Assert.True(
                MathF.Abs(expected - actual) <= tolerance,
                $"dim {dim}: expected {expected}, got {actual}");
        }
    }

    [Fact]
    public void Dot_throws_on_length_mismatch()
    {
        Assert.Throws<ArgumentException>(() => VectorMath.Dot(new float[3], new float[4]));
    }

    [Fact]
    public void Norm_and_normalize_are_consistent()
    {
        var vector = Vec(9, 32);
        var norm = VectorMath.Norm(vector);
        Assert.True(norm > 0f);

        var normalized = VectorMath.Normalize(vector);
        Assert.Equal(32, vector.Length); // input span untouched (copy-normalize)
        var normalizedNorm = VectorMath.Norm(normalized);
        Assert.Equal(1f, normalizedNorm, 4f);
    }

    [Fact]
    public void Zero_vector_stays_zero_and_cosine_is_zero()
    {
        var zero = new float[16];
        var normalized = VectorMath.Normalize(zero);
        Assert.All(normalized, static component => Assert.Equal(0f, component));

        var other = Vec(2, 16);
        Assert.Equal(0f, VectorMath.Cosine(zero, other));
        Assert.Equal(0f, VectorMath.Cosine(other, zero));
    }

    [Fact]
    public void Self_cosine_is_one()
    {
        var vector = Vec(4, 24);
        Assert.Equal(1f, VectorMath.Cosine(vector, vector), 4f);
    }

    [Fact]
    public void Cosine_is_symmetric_and_bounded()
    {
        var a = Vec(6, 20);
        var b = Vec(11, 20);
        var ab = VectorMath.Cosine(a, b);
        var ba = VectorMath.Cosine(b, a);
        Assert.Equal(ab, ba, 5f);
        Assert.InRange(ab, -1f, 1f);
    }
}

/// <summary>VectorItemStore semantics: upsert, swap-remove, snapshot consistency.</summary>
public sealed class VectorItemStoreTests
{
    private static VectorItemStore StoreWithItems(params (string Id, float V)[] items)
    {
        var store = new VectorItemStore(2);
        foreach (var (id, v) in items)
        {
            store.Set(id, new[] { v, v });
        }

        return store;
    }

    [Fact]
    public void Set_upserts_by_id()
    {
        var store = new VectorItemStore(3);
        store.Set("x", new[] { 1f, 2f, 3f });
        store.Set("x", new[] { 9f, 9f, 9f });
        Assert.Equal(1, store.Count);
        Assert.Equal(new[] { 9f, 9f, 9f }, store.TryGetVector("x"));
    }

    [Fact]
    public void Set_dim_mismatch_throws()
    {
        var store = new VectorItemStore(3);
        Assert.Throws<VectorIndexGuardMismatchException>(() => store.Set("x", new[] { 1f, 2f }));
    }

    [Fact]
    public void Remove_swaps_last_row_and_keeps_snapshot_aligned()
    {
        var store = StoreWithItems(("a", 1f), ("b", 2f), ("c", 3f), ("d", 4f));
        Assert.True(store.Remove("b"));
        Assert.False(store.Remove("b"));
        Assert.Equal(3, store.Count);

        var (ids, rows) = store.SnapshotRows();
        Assert.Equal(ids.Length, rows.Length);
        for (var i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            var vector = store.TryGetVector(id)!;
            Assert.Equal(vector, rows[i]);
            Assert.Equal(rows[i][0], rows[i][1]);
        }

        Assert.DoesNotContain("b", ids);
    }

    [Fact]
    public void Clear_empties_store()
    {
        var store = StoreWithItems(("a", 1f), ("b", 2f));
        store.Clear();
        Assert.Equal(0, store.Count);
        Assert.Null(store.TryGetVector("a"));
    }
}

/// <summary>VectorTopKBuffer heap behavior.</summary>
public sealed class VectorTopKBufferTests
{
    [Fact]
    public void Retains_best_k_in_descending_order_with_stable_tiebreak()
    {
        var top = new VectorTopKBuffer(3);
        var entries = new List<(string Id, float Score)>
        {
            ("a", 0.5f), ("b", 0.9f), ("c", 0.1f), ("d", 0.9f), ("e", 0.7f),
        };
        foreach (var entry in entries)
        {
            top.Offer(entry.Id, entry.Score);
        }

        var drained = top.Drain();
        Assert.Equal(new[] { "b", "d", "e" }, drained.Select(static entry => entry.Id).ToArray());
    }

    [Fact]
    public void Zero_capacity_drops_everything()
    {
        var top = new VectorTopKBuffer(0);
        top.Offer("a", 1f);
        Assert.Empty(top.Drain());
    }
}