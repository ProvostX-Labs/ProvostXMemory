using System;
using System.Collections.Generic;

namespace ProvostXMemory.VectorIndex.Gpu;

/// <summary>
/// Kernel-launch policy: rows per launch chunk and the per-launch thread cap. Chunked
/// launches keep the per-thread flat loop short and bounded on every backend (including
/// ILGPU's CPU simulator, which materializes one thread per index).
/// </summary>
public sealed class GpuLaunchOptions
{
    /// <summary>Conservative default: 65,536 rows per launch chunk.</summary>
    public const int DefaultRowsPerLaunch = 65_536;

    /// <summary>Conservative default: 65,536 threads per launch.</summary>
    public const int DefaultMaxThreadsPerLaunch = 65_536;

    public GpuLaunchOptions()
        : this(DefaultRowsPerLaunch, DefaultMaxThreadsPerLaunch)
    {
    }

    public GpuLaunchOptions(int rowsPerLaunch, int maxThreadsPerLaunch)
    {
        if (rowsPerLaunch <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowsPerLaunch));
        }

        if (maxThreadsPerLaunch <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxThreadsPerLaunch));
        }

        RowsPerLaunch = rowsPerLaunch;
        MaxThreadsPerLaunch = maxThreadsPerLaunch;
    }

    public int RowsPerLaunch { get; }

    public int MaxThreadsPerLaunch { get; }
}