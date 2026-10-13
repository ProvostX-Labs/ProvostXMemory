using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProvostXMemory.VectorIndex.Engine;

/// <summary>
/// Own-engine IVF-Flat: deterministic k-means coarse quantizer + inverted lists;
/// search probes the nprobe nearest clusters (by quantizer metric) and scores members
/// exactly. Mutations mark the partition stale; the next search rebuilds it (batch-rebuild
/// semantics per the 2026-10-13 addendum). Flush is a no-op (in-memory).
/// </summary>
public sealed class IvfFlatIndex : IVectorIndex
{
    private readonly object _lock = new();
    private readonly VectorItemStore _store;
    private readonly IVectorIndexGuard _guard;
    private readonly VectorScanner _searchScanner;
    private readonly KMeansQuantizer _quantizer;
    private readonly Options _options;
    private volatile IvfPostingLists? _lists;

    public IvfFlatIndex(IVectorIndexGuard guard)
        : this(guard, new Options())
    {
    }

    public IvfFlatIndex(IVectorIndexGuard guard, Options options)
    {
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(options);
        _guard = guard;
        _options = options;
        _store = new VectorItemStore(guard.Dim);
        _searchScanner = new VectorScanner(options.Metric);
        _quantizer = new KMeansQuantizer(options.Seed);
    }

    /// <summary>Backend options: metric, cluster count, probe count, k-means effort, deterministic seed.</summary>
    public sealed class Options
    {
        public Options()
            : this(nlist: 8, nprobe: 2, kmeansIterations: 25, seed: 1729)
        {
        }

        public Options(int nlist, int nprobe, int kmeansIterations, int seed)
            : this(nlist, nprobe, kmeansIterations, seed, VectorDistanceMetric.Cosine)
        {
        }

        public Options(int nlist, int nprobe, int kmeansIterations, int seed, VectorDistanceMetric metric)
        {
            if (nlist <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nlist));
            }

            if (nprobe <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nprobe));
            }

            if (kmeansIterations < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(kmeansIterations));
            }

            Nlist = nlist;
            Nprobe = nprobe;
            KmeansIterations = kmeansIterations;
            Seed = seed;
            Metric = metric;
        }

        public int Nlist { get; }

        public int Nprobe { get; }

        public int KmeansIterations { get; }

        public int Seed { get; }

        public VectorDistanceMetric Metric { get; }
    }

    public int Dim => _guard.Dim;

    public long Count => _store.Count;

    /// <summary>Current partition state; null until first build/rebuild with data.</summary>
    public int ClusterCount
    {
        get
        {
            lock (_lock)
            {
                return _lists?.ClusterCount ?? 0;
            }
        }
    }

    public Task BuildAsync(IReadOnlyList<VectorItem> items, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                lock (_lock)
                {
                    _store.Clear();
                    foreach (var item in items)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _store.Set(item.Id, CopyChecked(item));
                    }

                    RebuildPartitionLocked();
                }
            },
            cancellationToken);
    }

    public Task AddAsync(VectorItem item, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                lock (_lock)
                {
                    _store.Set(item.Id, CopyChecked(item));
                    MarkStaleLocked();
                }
            },
            cancellationToken);
    }

    public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                lock (_lock)
                {
                    if (!_store.Remove(id))
                    {
                        throw new VectorIndexIdNotFoundException(id);
                    }

                    MarkStaleLocked();
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
                lock (_lock)
                {
                    var lists = _lists;
                    if (lists is null)
                    {
                        RebuildPartitionLocked();
                        lists = _lists;
                    }

                    if (lists is null || lists.ClusterCount == 0)
                    {
                        return (IReadOnlyList<ScoredVectorId>)Array.Empty<ScoredVectorId>();
                    }

                    var probeOrder = lists.ProbeOrder(query, _searchScanner);
                    var nprobe = Math.Min(_options.Nprobe, probeOrder.Count);
                    var top = new VectorTopKBuffer(k);

                    for (var probe = 0; probe < nprobe; probe++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var cluster = probeOrder[probe].Cluster;
                        foreach (var memberIndex in lists.MemberIndicesOf(cluster))
                        {
                            var id = lists.IdOfMember(memberIndex);
                            if (!_store.Contains(id))
                            {
                                continue; // stale posting entry removed meanwhile
                            }

                            var score = _searchScanner.Score(query, lists.VectorOfMember(memberIndex));
                            top.Offer(id, score);
                        }
                    }

                    var drained = top.Drain();
                    var hits = new List<ScoredVectorId>(drained.Count);
                    foreach (var (id, score) in drained)
                    {
                        hits.Add(new ScoredVectorId(id, score));
                    }

                    return (IReadOnlyList<ScoredVectorId>)hits;
                }
            },
            cancellationToken);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        // In-memory backend; partition is rebuilt from the store on demand, nothing to persist.
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
            },
            cancellationToken);
    }

    /// <summary>Cluster count of the last built partition (0 when empty).</summary>
    private void RebuildPartitionLocked()
    {
        var (ids, rows) = _store.SnapshotRows();
        if (rows.Length == 0)
        {
            _lists = null;
            return;
        }

        var clusterCount = Math.Min(_options.Nlist, rows.Length);
        KMeansResult result;
        if (clusterCount == 1)
        {
            result = new KMeansResult(new[] { (float[])rows[0].Clone() }, new int[rows.Length]);
        }
        else
        {
            result = _quantizer.Cluster(rows, clusterCount, _options.KmeansIterations);
            // Determinism guarantee: same rows + seed → same partition (k < rows count).
        }

        var members = new List<IvfMember>(rows.Length);
        for (var r = 0; r < rows.Length; r++)
        {
            members.Add(new IvfMember(ids[r], rows[r], result.Labels[r]));
        }

        _lists = new IvfPostingLists(result.Centroids, members);
    }

    private void MarkStaleLocked()
    {
        // Batch-rebuild semantics: keep last partition for probing correctness only after
        // rebuild; membership changes always invalidate it.
        _lists = null;
    }

    private float[] CopyChecked(VectorItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Vector.Count != _guard.Dim)
        {
            throw new VectorIndexGuardMismatchException(
                $"Vector dimension mismatch: index dim {_guard.Dim} != item dim {item.Vector.Count} (id '{item.Id}').");
        }

        var buffer = new float[item.Vector.Count];
        for (var i = 0; i < item.Vector.Count; i++)
        {
            buffer[i] = item.Vector[i];
        }

        return buffer;
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