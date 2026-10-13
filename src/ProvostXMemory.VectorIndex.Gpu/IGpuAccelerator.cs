using ILGPU;
using ILGPU.Runtime;

namespace ProvostXMemory.VectorIndex.Gpu;

/// <summary>
/// Availability handle for the ILGPU runtime: owns the Context and exposes the resolved
/// accelerator lazily. Construction never throws and never touches GPU drivers; every
/// member is guarded so machines without CUDA/OpenCL observe IsAvailable=false.
/// </summary>
public interface IGpuAccelerator : IDisposable
{
    /// <summary>True when a GPU accelerator was resolved at hand-shake time.</summary>
    bool IsAvailable { get; }

    /// <summary>Short backend name (e.g. 'cuda', 'opencl'); empty when unavailable.</summary>
    string Name { get; }

    /// <summary>Human-readable device description; empty when unavailable.</summary>
    string DeviceDescription { get; }

    /// <summary>
    /// The resolved accelerator for kernel loads/launches; throws when IsAvailable is false.
    /// </summary>
    Accelerator RequireAccelerator();
}