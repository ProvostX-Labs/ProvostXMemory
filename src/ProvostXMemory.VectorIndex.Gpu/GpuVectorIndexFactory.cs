using System;

using ProvostXMemory.Abstractions;
using ProvostXMemory.VectorIndex;

namespace ProvostXMemory.VectorIndex.Gpu;

/// <summary>
/// Factory that produces <see cref="GpuFlatIndex"/> for <see cref="IndexKind.Gpu"/> and
/// delegates every other kind to the injected CPU <see cref="IVectorIndexFactory"/>. The
/// GPU index receives the standard <see cref="IGpuAccelerator"/> availability gate and
/// wraps a flat CPU fallback <see cref="IVectorIndex"/> as its transparent CPU fallback,
/// so search semantics are identical whether or not an accelerator is present.
/// </summary>
public sealed class GpuVectorIndexFactory : IVectorIndexFactory
{
    private readonly IGpuAccelerator _gpu;
    private readonly IVectorIndexFactory _cpu;

    public GpuVectorIndexFactory(IGpuAccelerator gpu, IVectorIndexFactory cpuFactory)
    {
        ArgumentNullException.ThrowIfNull(gpu);
        ArgumentNullException.ThrowIfNull(cpuFactory);
        _gpu = gpu;
        _cpu = cpuFactory;
    }

    public IVectorIndex Create(IndexKind kind, int dim)
    {
        if (kind == IndexKind.Gpu)
        {
            var guard = new VectorIndexGuard("gpu-factory", dim);
            var fallback = _cpu.Create(IndexKind.Flat, dim);
            return new GpuFlatIndex(_gpu, guard, fallback);
        }

        return _cpu.Create(kind, dim);
    }
}
