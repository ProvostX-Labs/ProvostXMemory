using System;

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

namespace ProvostXMemory.VectorIndex.Gpu;

/// <summary>
/// How the ILGPU context is built for accelerator discovery. GPU-first builders enable
/// CUDA/OpenCL discovery; the flag set mirrors what the host considers acceptable.
/// </summary>
public sealed class GpuDiscoveryOptions
{
    public GpuDiscoveryOptions()
        : this(tryCuda: true, tryOpenCl: true)
    {
    }

    public GpuDiscoveryOptions(bool tryCuda, bool tryOpenCl)
    {
        TryCuda = tryCuda;
        TryOpenCl = tryOpenCl;
    }

    public bool TryCuda { get; }

    public bool TryOpenCl { get; }
}