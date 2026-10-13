using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.VectorIndex;

/// <summary>
/// Creates vector indexes of a requested <see cref="IndexKind"/> for one dimension.
/// Implementations decide concrete wiring (metrics, seeds, backing storage); the factory
/// keeps index creation out of retrieval code so backends stay swappable by configuration
/// alone. The default <c>EngineVectorIndexFactory</c> produces the own-engine backends;
/// the GPU factory decorates a CPU fallback with ILGPU kernels.
/// </summary>
public interface IVectorIndexFactory
{
    /// <summary>Creates an index of <paramref name="kind"/> for the given dimension.</summary>
    IVectorIndex Create(IndexKind kind, int dim);
}