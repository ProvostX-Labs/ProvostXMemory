using System;

using ILGPU;
using ILGPU.Runtime;

namespace ProvostXMemory.VectorIndex.Gpu;

/// <summary>
/// GPU-resident row-major vector matrix with lazy (re)upload: CPU-side rows + norms are
/// mirrored to device buffers; uploads overwrite exactly the changed range. Ownership
/// and synchronization live here — indexes never touch ILGPU buffers directly.
/// </summary>
public sealed class GpuVectorMatrix : IDisposable
{
    private readonly IGpuAccelerator _gpu;
    private readonly object _lock = new();

    private float[] _rows = Array.Empty<float>();
    private float[] _norms = Array.Empty<float>();
    private int _rowCount;
    private int _dim;
    private bool _rowsDirty = true;
    private MemoryBuffer1D<float, Stride1D.Dense>? _deviceRows;
    private MemoryBuffer1D<float, Stride1D.Dense>? _deviceNorms;
    private int _deviceRowCount;
    private int _deviceDim;

    public GpuVectorMatrix(IGpuAccelerator gpu)
    {
        ArgumentNullException.ThrowIfNull(gpu);
        _gpu = gpu;
    }

    public int RowCount
    {
        get
        {
            lock (_lock)
            {
                return _rowCount;
            }
        }
    }

    public int Dim
    {
        get
        {
            lock (_lock)
            {
                return _dim;
            }
        }
    }

    /// <summary>Replaces the whole CPU-side matrix (rows laid out row-major, dim-derived).</summary>
    public void ReplaceAll(float[] rows, int rowCount, int dim)
    {
        lock (_lock)
        {
            _rows = (float[])rows.Clone();
            _norms = ComputeNorms(rows, rowCount, dim);
            _rowCount = rowCount;
            _dim = dim;
            _rowsDirty = true;
        }
    }

    /// <summary>Reads back one CPU-side row (defensive copy) for CPU-fallback consumption.</summary>
    public float[] CopyRow(int row)
    {
        lock (_lock)
        {
            var dest = new float[_dim];
            Array.Copy(_rows, row * _dim, dest, 0, _dim);
            return dest;
        }
    }

    /// <summary>Row norm of one CPU-side row.</summary>
    public float NormOf(int row)
    {
        lock (_lock)
        {
            return _norms[row];
        }
    }

    /// <summary>CPU-side row count (source of truth for the mirror).</summary>
    public int TotalRows
    {
        get
        {
            lock (_lock)
            {
                return _rowCount;
            }
        }
    }

    /// <summary>
    /// Ensures the device mirrors CPU rows [0, rowCount) and returns the device row view.
    /// Uploads only the changed range; rebuilds from scratch after shrink-heavy edits.
    /// </summary>
    public ArrayView<float> EnsureDeviceRows()
    {
        lock (_lock)
        {
            if (_deviceRows is null
                || _deviceDim != _dim
                || _deviceRowCount < _rowCount)
            {
                RebuildDeviceBuffers();
            }
            else if (_rowCount > 0 && _rowsDirty)
            {
                UploadRows(0, _rowCount);
            }

            return _deviceRows!.View;
        }
    }

    /// <summary>Ensures the device norm mirror; returns the device norm view.</summary>
    public ArrayView<float> EnsureDeviceNorms()
    {
        EnsureDeviceRows();
        lock (_lock)
        {
            return _deviceNorms!.View;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _deviceRows?.Dispose();
            _deviceRows = null;
            _deviceNorms?.Dispose();
            _deviceNorms = null;
            _deviceRowCount = 0;
            _deviceDim = 0;
            _rows = Array.Empty<float>();
            _norms = Array.Empty<float>();
            _rowCount = 0;
            _dim = 0;
            _rowsDirty = true;
        }
        GC.SuppressFinalize(this);
    }

    private void RebuildDeviceBuffers()
    {
        _deviceRows?.Dispose();
        _deviceNorms?.Dispose();
        var accelerator = _gpu.RequireAccelerator();
        var capacity = Math.Max(_rowCount, 1);
        _deviceRows = accelerator.Allocate1D<float>(capacity);
        _deviceNorms = accelerator.Allocate1D<float>(capacity);
        _deviceDim = _dim;
        _deviceRowCount = 0;
        _rowsDirty = false;
        if (_rowCount > 0)
        {
            UploadRows(0, _rowCount);
        }
    }

    private void UploadRows(int start, int end)
    {
        if (end > _deviceRows!.Extent.X)
        {
            RebuildDeviceBuffers();
        }

        // Host slices are copied via SubView views (CopyFromCPU overwrites exactly the range).
        var rowSlice = new float[(end - start) * _dim];
        Array.Copy(_rows, start * _dim, rowSlice, 0, rowSlice.Length);
        _deviceRows!.View.SubView(start, end - start).CopyFromCPU(rowSlice);
        var normSlice = new float[end - start];
        Array.Copy(_norms, start, normSlice, 0, end - start);
        _deviceNorms!.View.SubView(start, end - start).CopyFromCPU(normSlice);
        _deviceRowCount = Math.Max(_deviceRowCount, end);
        _rowsDirty = false;
    }

    /// <summary>True when the device mirror covers every live row; false after removals/shrinks.</summary>
    public bool DeviceInSync()
    {
        lock (_lock)
        {
            return _deviceRows is not null
                && _deviceDim == _dim
                && _deviceRowCount >= _rowCount
                && !_rowsDirty;
        }
    }

    private static float[] ComputeNorms(float[] rows, int rowCount, int dim)
    {
        var norms = new float[rowCount];
        for (var row = 0; row < rowCount; row++)
        {
            var sum = 0f;
            var baseOffset = row * dim;
            for (var d = 0; d < dim; d++)
            {
                var v = rows[baseOffset + d];
                sum += v * v;
            }

            norms[row] = MathF.Sqrt(sum);
        }

        return norms;
    }
}