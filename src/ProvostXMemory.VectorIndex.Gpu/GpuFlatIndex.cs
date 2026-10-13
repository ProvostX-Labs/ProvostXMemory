using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ILGPU.Runtime;

using ProvostXMemory.VectorIndex;

namespace ProvostXMemory.VectorIndex.Gpu;

/// <summary>
/// GPU-accelerated exact flat index (ILGPU kernel scan). When no GPU is available the
/// injected CPU fallback IVectorIndex is used for every operation with identical
/// semantics — the fallback path must pass the engine conformance suite unchanged.
/// Kernels load lazily; all GPU faults collapse to CPU-fallback execution.
/// </summary>
public sealed class GpuFlatIndex : IVectorIndex
{
    private readonly IGpuAccelerator _gpu;
    private readonly IGpuScoreLauncher _launcher;
    private readonly IVectorIndex _fallback;
    private readonly IVectorIndexGuard _guard;
    private readonly VectorDistanceMetric _metric;
    private readonly VectorItemStore _store;
    private readonly GpuVectorMatrix _matrix;
    private readonly object _lock = new();

    // Convenience for hosts/tests that just want GPU-with-fallback semantics.
    public GpuFlatIndex(IVectorIndexGuard guard, IVectorIndex fallback)
        : this(new GpuAccelerator(), guard, fallback)
    {
    }

    public GpuFlatIndex(
        IGpuAccelerator gpu,
        IVectorIndexGuard guard,
        IVectorIndex fallback)
        : this(gpu, guard, fallback, new Options())
    {
    }

    public GpuFlatIndex(
        IGpuAccelerator gpu,
        IVectorIndexGuard guard,
        IVectorIndex fallback,
        Options options)
        : this(gpu, new IlgpuScoreLauncher(gpu, new GpuLaunchOptions()), guard, fallback, options)
    {
    }

    public GpuFlatIndex(
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
        _metric = options.Metric;
        _store = new VectorItemStore(guard.Dim);
        _matrix = new GpuVectorMatrix(gpu);
    }

    /// <summary>Backend options: ranking metric (default cosine) + launch chunking.</summary>
    public sealed class Options
    {
        public Options()
            : this(VectorDistanceMetric.Cosine)
        {
        }

        public Options(VectorDistanceMetric metric)
            : this(metric, new GpuLaunchOptions())
        {
        }

        public Options(VectorDistanceMetric metric, GpuLaunchOptions launch)
        {
            ArgumentNullException.ThrowIfNull(launch);
            Metric = metric;
            Launch = launch;
        }

        public VectorDistanceMetric Metric { get; }

        public GpuLaunchOptions Launch { get; }
    }

    public int Dim => _guard.Dim;

    public long Count => _store.Count;

    /// <summary>True when scoring would run on a GPU device (not the CPU fallback).</summary>
    public bool UsesGpu => _gpu.IsAvailable;

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

                    RebuildMatrixLocked();

                    if (!_gpu.IsAvailable)
                    {
                        // CPU fallback lives under the same lock so both stores never diverge.
                        _fallback.BuildAsync(SnapshotItemsLocked(), cancellationToken)
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
                    RebuildMatrixLocked();
                    if (!_gpu.IsAvailable)
                    {
                        _fallback.AddAsync(new VectorItem(item.Id, _matrix.CopyRow(RowOfLocked(item.Id))), cancellationToken)
                            .GetAwaiter().GetResult();
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

                    RebuildMatrixLocked();
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
                    var rowCount = (int)_store.Count;
                    if (rowCount == 0)
                    {
                        return (IReadOnlyList<ScoredVectorId>)Array.Empty<ScoredVectorId>();
                    }

                    try
                    {
                        return (IReadOnlyList<ScoredVectorId>)SearchGpuLocked(query, k, rowCount, cancellationToken);
                    }
                    catch (Exception)
                    {
                        // Transparent degradation: any GPU-side fault falls back to CPU.
                        return _fallback.SearchAsync(query, k, cancellationToken).GetAwaiter().GetResult();
                    }
                }
            },
            cancellationToken);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        // In-memory backend: Flush validates cancellation only (identical to engine Flat).
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

    private IReadOnlyList<ScoredVectorId> SearchGpuLocked(
        float[] query,
        int k,
        int rowCount,
        CancellationToken cancellationToken)
    {
        var (ids, _) = _store.SnapshotRows();
        var rowsView = _matrix.EnsureDeviceRows();
        var normsView = _matrix.EnsureDeviceNorms();

        if (!_matrix.DeviceInSync())
        {
            // Mirror lags live rows: rebuild it before scoring.
            RebuildMatrixLocked();
            rowsView = _matrix.EnsureDeviceRows();
            normsView = _matrix.EnsureDeviceNorms();
        }

        var scores = new float[rowCount];
        var accelerator = _gpu.RequireAccelerator();
        using var queryBuffer = accelerator.Allocate1D<float>(query.Length);
        queryBuffer.CopyFromCPU(query);
        using var scoreBuffer = accelerator.Allocate1D<float>(rowCount);
        _launcher.ScoreRows(
            queryBuffer.View,
            rowsView,
            normsView,
            scoreBuffer.View,
            Dim,
            GpuMetricCodec.Encode(_metric),
            QueryNorm(query),
            0,
            rowCount);
        scoreBuffer.CopyToCPU(scores);

        var top = new VectorTopKBuffer(k);
        for (var row = 0; row < rowCount; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            top.Offer(ids[row], scores[row]);
        }

        var drained = top.Drain();
        var hits = new List<ScoredVectorId>(drained.Count);
        foreach (var (id, score) in drained)
        {
            hits.Add(new ScoredVectorId(id, score));
        }

        return hits;
    }

    private void RebuildMatrixLocked()
    {
        var (ids, rows) = _store.SnapshotRows();
        var rowCount = rows.Length;
        if (rowCount == 0)
        {
            _matrix.ReplaceAll(Array.Empty<float>(), 0, Dim);
            return;
        }

        var flat = new float[rowCount * Dim];
        for (var row = 0; row < rowCount; row++)
        {
            rows[row].CopyTo(flat, row * Dim);
        }

        _matrix.ReplaceAll(flat, rowCount, Dim);
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