using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProvostXMemory.VectorIndex.Engine;

/// <summary>
/// Own-engine exact backend: SIMD brute-force scan (Vector512→256→128, scalar fallback)
/// over the in-memory float[] store. Fully built in memory; Flush is a no-op.
/// </summary>
public sealed class FlatExactIndex : IVectorIndex
{
    private readonly VectorItemStore _store;
    private readonly IVectorIndexGuard _guard;
    private readonly VectorScanner _scanner;

    public FlatExactIndex(IVectorIndexGuard guard)
        : this(guard, new Options())
    {
    }

    public FlatExactIndex(IVectorIndexGuard guard, Options options)
    {
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(options);
        _guard = guard;
        _store = new VectorItemStore(guard.Dim);
        _scanner = new VectorScanner(options.Metric);
    }

    /// <summary>Backend options: the ranking metric (default cosine).</summary>
    public sealed class Options
    {
        public Options()
            : this(VectorDistanceMetric.Cosine)
        {
        }

        public Options(VectorDistanceMetric metric)
        {
            Metric = metric;
        }

        public VectorDistanceMetric Metric { get; }
    }

    public int Dim => _guard.Dim;

    public long Count => _store.Count;

    public Task BuildAsync(IReadOnlyList<VectorItem> items, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                _store.Clear();
                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _store.Set(item.Id, CopyChecked(item));
                }
            },
            cancellationToken);
    }

    public Task AddAsync(VectorItem item, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                _store.Set(item.Id, CopyChecked(item));
            },
            cancellationToken);
    }

    public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                if (!_store.Remove(id))
                {
                    throw new VectorIndexIdNotFoundException(id);
                }
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<ScoredVectorId>> SearchAsync(
        IReadOnlyList<float> vector,
        int k,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                if (k < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(k));
                }

                var query = CopyCheckedQuery(vector);
                var (ids, rows) = _store.SnapshotRows();
                var top = new VectorTopKBuffer(k);
                for (var row = 0; row < rows.Length; row++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    top.Offer(ids[row], _scanner.Score(query, rows[row]));
                }

                var drained = top.Drain();
                var hits = new List<ScoredVectorId>(drained.Count);
                foreach (var (id, score) in drained)
                {
                    hits.Add(new ScoredVectorId(id, score));
                }

                return (IReadOnlyList<ScoredVectorId>)hits;
            },
            cancellationToken);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        // Fully in-memory backend: nothing to persist; FlushAsync validates cancellation only.
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
            },
            cancellationToken);
    }

    private float[] CopyChecked(VectorItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var buffer = new float[item.Vector.Count];
        for (var i = 0; i < item.Vector.Count; i++)
        {
            buffer[i] = item.Vector[i];
        }

        return buffer; // dim check happens in VectorItemStore.Set (single source of truth)
    }

    private float[] CopyCheckedQuery(IReadOnlyList<float> vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        if (vector.Count != _guard.Dim)
        {
            throw new VectorIndexGuardMismatchException(
                $"Vector dimension mismatch: index dim {_guard.Dim} != query dim {vector.Count}.");
        }

        var buffer = new float[vector.Count];
        for (var i = 0; i < vector.Count; i++)
        {
            buffer[i] = vector[i];
        }

        return buffer;
    }
}