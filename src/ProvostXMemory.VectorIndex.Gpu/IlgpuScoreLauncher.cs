using System;

using ILGPU;
using ILGPU.Runtime;

namespace ProvostXMemory.VectorIndex.Gpu;

/// <summary>
/// Lazy, guarded ILGPU kernel launcher: kernels compile on first launch only (never at
/// construction), launches are chunked per GpuLaunchOptions, and every launch ends with
/// a synchronize so scored results are deterministically visible on the host.
/// </summary>
public sealed class IlgpuScoreLauncher : IGpuScoreLauncher
{
    private readonly IGpuAccelerator _gpu;
    private readonly object _loadLock = new();
    private readonly GpuLaunchOptions _launchOptions;

    private Action<
        AcceleratorStream,
        Index1D,
        ArrayView<float>,
        ArrayView<float>,
        ArrayView<float>,
        ArrayView<float>,
        int,
        int,
        float,
        int>? _scoreRowsKernel;
    private Action<
        AcceleratorStream,
        Index1D,
        ArrayView<float>,
        ArrayView<float>,
        ArrayView<float>,
        ArrayView<float>,
        ArrayView<int>,
        int,
        int,
        float,
        int>? _scoreIndexedRowsKernel;

    public IlgpuScoreLauncher(IGpuAccelerator gpu, GpuLaunchOptions launchOptions)
    {
        ArgumentNullException.ThrowIfNull(gpu);
        ArgumentNullException.ThrowIfNull(launchOptions);
        _gpu = gpu;
        _launchOptions = launchOptions;
    }

    public void ScoreRows(
        ArrayView<float> query,
        ArrayView<float> rows,
        ArrayView<float> rowNorms,
        ArrayView<float> scores,
        int dim,
        int metric,
        float queryNorm,
        int rowOffset,
        int rowEnd)
    {
        var kernel = EnsureScoreRowsKernel();
        LaunchChunked(rowOffset, rowEnd, (chunkStart, chunkCount) =>
            kernel(
                _gpu.RequireAccelerator().DefaultStream,
                new Index1D(chunkCount),
                query,
                rows,
                rowNorms,
                scores,
                dim,
                metric,
                queryNorm,
                chunkStart));
    }

    public void ScoreIndexedRows(
        ArrayView<float> query,
        ArrayView<float> rows,
        ArrayView<float> rowNorms,
        ArrayView<float> scores,
        ArrayView<int> memberRows,
        int dim,
        int metric,
        float queryNorm,
        int candidateOffset,
        int candidateEnd)
    {
        var kernel = EnsureScoreIndexedRowsKernel();
        LaunchChunked(candidateOffset, candidateEnd, (chunkStart, chunkCount) =>
            kernel(
                _gpu.RequireAccelerator().DefaultStream,
                new Index1D(chunkCount),
                query,
                rows,
                rowNorms,
                scores,
                memberRows,
                dim,
                metric,
                queryNorm,
                chunkStart));
    }

    private Action<
        AcceleratorStream,
        Index1D,
        ArrayView<float>,
        ArrayView<float>,
        ArrayView<float>,
        ArrayView<float>,
        int,
        int,
        float,
        int> EnsureScoreRowsKernel()
    {
        lock (_loadLock)
        {
            _scoreRowsKernel ??= _gpu.RequireAccelerator()
                .LoadAutoGroupedKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>, int, int, float, int>(
                    GpuVectorKernels.ScoreRowsKernel);
            return _scoreRowsKernel;
        }
    }

    private Action<
        AcceleratorStream,
        Index1D,
        ArrayView<float>,
        ArrayView<float>,
        ArrayView<float>,
        ArrayView<float>,
        ArrayView<int>,
        int,
        int,
        float,
        int> EnsureScoreIndexedRowsKernel()
    {
        lock (_loadLock)
        {
            _scoreIndexedRowsKernel ??= _gpu.RequireAccelerator()
                .LoadAutoGroupedKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<int>, int, int, float, int>(
                    GpuVectorKernels.ScoreIndexedRowsKernel);
            return _scoreIndexedRowsKernel;
        }
    }

    /// <summary>Runs action in bounded chunks: [start, end) split by RowsPerLaunch × threads.</summary>
    private void LaunchChunked(int start, int end, Action<int, int> action)
    {
        if (end <= start)
        {
            return;
        }

        var chunk = Math.Max(1, Math.Min(_launchOptions.RowsPerLaunch, _launchOptions.MaxThreadsPerLaunch));
        var accelerator = _gpu.RequireAccelerator();
        if (accelerator.MaxNumThreadsPerGroup > 0)
        {
            chunk = Math.Min(chunk, accelerator.MaxNumThreadsPerGroup * 1024);
        }
        for (var from = start; from < end; from += chunk)
        {
            var count = Math.Min(chunk, end - from);
            action(from, count);
        }

        accelerator.Synchronize();
    }
}