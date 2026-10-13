using System;

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

namespace ProvostXMemory.VectorIndex.Gpu;

/// <summary>
/// ILGPU accelerator hand-shake: a GPU-first context (CUDA + OpenCL as configured) with
/// device classes the caller considers acceptable. All discovery is exception-
/// guarded; a machine without GPU drivers simply observes IsAvailable=false.
/// </summary>
public interface IGpuContextPolicy
{
    /// <summary>Device kinds that count as 'GPU' for availability purposes.</summary>
    bool Accepts(AcceleratorType acceleratorType);

    /// <summary>Builds the ILGPU context per the discovery options.</summary>
    Context CreateContext(GpuDiscoveryOptions discovery);

    /// <summary>Picks the accelerator device to use, or null when none is acceptable.</summary>
    Device? SelectDevice(Context context, GpuDiscoveryOptions discovery);
}