using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ILGPU.Runtime;

using ProvostXMemory.VectorIndex;

namespace ProvostXMemory.VectorIndex.Gpu;

/// <summary>
/// GPU-accelerated IVF index: the deterministic engine k-means partitions the corpus
/// (CPU-side, same algorithm as IvfFlatIndex so partitions are identical), GPU kernels
/// score (1) coarse centroids and (2) in-cluster members. When no GPU is available the
/// injected CPU fallback IVectorIndex is used with identical semantics.
/// </summary>
public sealed class GpuIvfIndex : IVectorIndex
{
    private readonly IGpuAccelerator _gpu;
    private readonly IGpuScoreLauncher _launcher;
    private readonly IVectorIndex _fallback;
    private readonly IVectorIndexGuard _guard;
    private readonly VectorDistanceMetric _metric;
    private readonly Options _options;
    private readonly VectorItemStore _store;
    private readonly GpuVectorMatrix _matrix;
    private readonly GpuVectorMatrix _centroids;
    private readonly KMeansQuantizer _quantizer;
    private readonly object _lock = new();
    private IvfPostingLists? _lists;

    public GpuIvfIndex(
        IGpuAccelerator gpu,
        IVectorIndexGuard guard,
        IVectorIndex fallback)
        : this(gpu, guard, fallback, new Options())
    {
    }

    public GpuIvfIndex(
        IGpuAccelerator gpu,
        IVectorIndexGuard guard,
        IVectorIndex fallback,
        Options options)
        : this(gpu, new IlgpuScoreLauncher(gpu, new GpuLaunchOptions()), guard, fallback, options)
    {
    }

    public GpuIvfIndex(
        IGpuAccelerator gpu,
        IGpuScoreLauncher launcher,
        IVectorIndexGuard guard,
        IVectorIndex fallback,
        Options options)
    {
        ArgumentNullException.ThrowIfNull(gpu);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(options);
        _gpu = gpu;
        _launcher = launcher;
        _guard = guard;
        _fallback = fallback;
        _options = options;
        _metric = options.Metric;
        _store = new VectorItemStore(guard.Dim);
        _matrix = new GpuVectorMatrix(gpu);
        _centroids = new GpuVectorMatrix(gpu);
        _quantizer = new KMeansQuantizer(options.Seed);
    }

    /// <summary>Backend options: mirrors the engine IVF options + ranking metric + launch chunking.</summary>
    public sealed class Options
    {
        public Options()
            : this(nlist: 8, nprobe: 2, kmeansIterations: 25, seed: 1729)
        {
        }

        public Options(int nlist, int nprobe, int kmeansIterations, int seed)
            : this(nlist, nprobe, kmeansIterations, seed, VectorDistanceMetric.Cosine, new GpuLaunchOptions())
        {
        }

        public Options(
            int nlist,
            int nprobe,
            int kmeansIterations,
            int seed,
            VectorDistanceMetric metric,
            GpuLaunchOptions launch)
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

            ArgumentNullException.ThrowIfNull(launch);
            Nlist = nlist;
            Nprobe = nprobe;
            KmeansIterations = kmeansIterations;
            Seed = seed;
            Metric = metric;
            Launch = launch;
        }

        public int Nlist { get; }

        public int Nprobe { get; }

        public int KmeansIterations { get; }

        public int Seed { get; }

        public VectorDistanceMetric Metric { get; }

        public GpuLaunchOptions Launch { get; }
    }

    public int Dim => _guard.Dim;

    public long Count => _store.Count;

    /// <summary>True when scanning runs on a GPU device (not the CPU fallback).</summary>
    public bool UsesGpu => _gpu.IsAvailable;

    /// <summary>Cluster count of the last built partition (0 when empty).</summary>
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

                    RebuildMatrixAndPartitionLocked();

                    if (!_gpu.IsAvailable)
                    {
                        _fallback.BuildAsync(SnapshotItemsLocked(),cancellationToken)
                            .GetAwaiter().GetResult();
                    }
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
                    RebuildMatrixAndPartitionLocked();
                    if (!_gpu.IsAvailable)
                    {
                        _fallback.AddAsync(
                            new VectorItem(item.Id, _matrix.CopyRow(RowOfLocked(item.Id))),
                            cancellationToken).GetAwaiter().GetResult();
                    }
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

                    RebuildMatrixAndPartitionLocked();
                    if (!_gpu.IsAvailable)
                    {
                        _fallback.RemoveAsync(id, cancellationToken).GetAwaiter().GetResult();
                    }
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
                if (!_gpu.IsAvailable)
                {
                    return _fallback.SearchAsync(query, k, cancellationToken).GetAwaiter().GetResult();
                }

                lock (_lock)
                {
                    var lists = _lists;
                    if (lists is null || lists.ClusterCount == 0)
                    {
                        return (IReadOnlyList<ScoredVectorId>)Array.Empty<ScoredVectorId>();
                    }

                    if (!_matrix.DeviceInSync())
                    {
                        RebuildMatrixAndPartitionLocked();
                        lists = _lists;
                        if (lists is null || lists.ClusterCount == 0)
                        {
                            return (IReadOnlyList<ScoredVectorId>)Array.Empty<ScoredVectorId>();
                        }
                    }

                    var metric = GpuMetricCodec.Encode(_metric);
                    var queryNorm = QueryNorm(query);

                    // 1) coarse assignment: score centroids on device, pick nprobe best.
                    var centroidScores = ScoreCentroids(lists, query, metric, queryNorm);
                    var probeOrder = OrderProbes(centroidScores);
                    var nprobe = Math.Min(_options.Nprobe, probeOrder.Count);

                    // 2) per-cluster scan: gather member rows across probed clusters.
                    var (memberRows, clusterOf) = GatherProbedMembers(lists, _store, probeOrder, nprobe);
                    var candidateCount = memberRows.Count;
                    if (candidateCount == 0)
                    {
                        return (IReadOnlyList<ScoredVectorId>)Array.Empty<ScoredVectorId>();
                    }

                    var accelerator = _gpu.RequireAccelerator();
                    var rowsView = _matrix.EnsureDeviceRows();
                    var normsView = _matrix.EnsureDeviceNorms();
                    using var queryBuffer = accelerator.Allocate1D<float>(query.Length);
                    queryBuffer.CopyFromCPU(query);
                    using var memberBuffer = ILGPU.Runtime.ArrayViewExtensions.Allocate1D<int>(accelerator, memberRows.ToArray());
                    using var scoreBuffer = accelerator.Allocate1D<float>(candidateCount);

                    _launcher.ScoreIndexedRows(
                        queryBuffer.View,
                        rowsView,
                        normsView,
                        scoreBuffer.View,
                        memberBuffer.View,
                        Dim,
                        metric,
                        queryNorm,
                        0,
                        candidateCount);

                    var scores = new float[candidateCount];
                    scoreBuffer.CopyToCPU(scores);

                    var top = new VectorTopKBuffer(k);
                    for (var candidate = 0; candidate < candidateCount; candidate++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var matrixRow = memberRows[candidate];
                        if (matrixRow < 0 || matrixRow >= _store.Count)
                        {
                            continue; // stale posting entry removed meanwhile
                        }

                        var id = _store.IdAt(matrixRow);
                        top.Offer(id, scores[candidate]);
                    }

                    return (IReadOnlyList<ScoredVectorId>)DrainHits(top);
                }
            },
            cancellationToken);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        // In-memory backend; partition is rebuilt from the store on demand, nothing persisted.
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_gpu.IsAvailable)
                {
                    _fallback.FlushAsync(cancellationToken).GetAwaiter().GetResult();
                }
            },
            cancellationToken);
    }

    private float[] ScoreCentroids(
        IvfPostingLists lists,
        float[] query,
        int metric,
        float queryNorm)
    {
        var clusterCount = lists.ClusterCount;
        var centroidFlat = new float[clusterCount * Dim];
        for (var c = 0; c < clusterCount; c++)
        {
            lists.CentroidOf(c).CopyTo(centroidFlat, c * Dim);
        }

        _centroids.ReplaceAll(centroidFlat, clusterCount, Dim);
        var centroidView = _centroids.EnsureDeviceRows();
        var centroidNorms = _centroids.EnsureDeviceNorms();

        var accelerator = _gpu.RequireAccelerator();
        using var queryBuffer = accelerator.Allocate1D<float>(query.Length);
        queryBuffer.CopyFromCPU(query);
        using var centroidScores = accelerator.Allocate1D<float>(clusterCount);

        _launcher.ScoreRows(
            queryBuffer.View,
            centroidView,
            centroidNorms,
            centroidScores.View,
            Dim,
            metric,
            queryNorm,
            0,
            clusterCount);

        var result = new float[clusterCount];
        centroidScores.CopyToCPU(result);
        return result;
    }

    private static List<(int Cluster, float Score)> OrderProbes(float[] centroidScores)
    {
        var order = new List<(int Cluster, float Score)>(centroidScores.Length);
        for (var c = 0; c < centroidScores.Length; c++)
        {
            order.Add((c, centroidScores[c]));
        }

        order.Sort(static (a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : a.Cluster.CompareTo(b.Cluster);
        });

        return order;
    }

    private static (List<int> MemberRows, List<int> ClusterOf) GatherProbedMembers(
        IvfPostingLists lists,
        VectorItemStore store,
        List<(int Cluster, float Score)> probeOrder,
        int nprobe)
    {
        var memberRows = new List<int>();
        var clusterOf = new List<int>();
        for (var probe = 0; probe < nprobe; probe++)
        {
            var cluster = probeOrder[probe].Cluster;
            foreach (var memberIndex in lists.MemberIndicesOf(cluster))
            {
                var id = lists.IdOfMember(memberIndex);
                var row = store.RowOf(id);
                if (row < 0)
                {
                    continue; // stale posting entry removed meanwhile
                }

                memberRows.Add(row);
                clusterOf.Add(cluster);
            }
        }

        return (memberRows, clusterOf);
    }

    private static IReadOnlyList<ScoredVectorId> DrainHits(VectorTopKBuffer top)
    {
        var drained = top.Drain();
        var hits = new List<ScoredVectorId>(drained.Count);
        foreach (var (id, score) in drained)
        {
            hits.Add(new ScoredVectorId(id, score));
        }

        return hits;
    }

    private void RebuildMatrixAndPartitionLocked()
    {
        var (ids, rows) = _store.SnapshotRows();
        var rowCount = rows.Length;
        if (rowCount == 0)
        {
            _matrix.ReplaceAll(Array.Empty<float>(), 0, Dim);
            _lists = null;
            return;
        }

        var flat = new float[rowCount * Dim];
        for (var row = 0; row < rowCount; row++)
        {
            rows[row].CopyTo(flat, row * Dim);
        }

        _matrix.ReplaceAll(flat, rowCount, Dim);

        var clusterCount = Math.Min(_options.Nlist, rowCount);
        KMeansResult result;
        if (clusterCount == 1)
        {
            result = new KMeansResult(new[] { (float[])rows[0].Clone() }, new int[rowCount]);
        }
        else
        {
            result = _quantizer.Cluster(rows, clusterCount, _options.KmeansIterations);
        }

        var members = new List<IvfMember>(rowCount);
        for (var r = 0; r < rowCount; r++)
        {
            members.Add(new IvfMember(ids[r], rows[r], result.Labels[r]));
        }

        _lists = new IvfPostingLists(result.Centroids, members);
    }

    private IReadOnlyList<VectorItem> SnapshotItemsLocked()
    {
        var (ids, rows) = _store.SnapshotRows();
        var items = new List<VectorItem>(ids.Length);
        for (var i = 0; i < ids.Length; i++)
        {
            items.Add(new VectorItem(ids[i], rows[i]));
        }

        return items;
    }

    private int RowOfLocked(string id)
    {
        var row = _store.RowOf(id);
        if (row < 0)
        {
            throw new VectorIndexIdNotFoundException(id);
        }

        return row;
    }

    private static float QueryNorm(float[] query)
    {
        var sum = 0f;
        foreach (var value in query)
        {
            sum += value * value;
        }

        return MathF.Sqrt(sum);
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