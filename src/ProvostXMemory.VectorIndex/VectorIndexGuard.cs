using System;

namespace ProvostXMemory.VectorIndex;

/// <summary>Guard record binding a vector namespace to an embedder model hash/dim.</summary>
public sealed record VectorIndexGuard(string ModelHash, int Dim) : IVectorIndexGuard
{
    /// <summary>Throws <see cref="VectorIndexGuardMismatchException"/> on model-hash or dim mismatch.</summary>
    public void Ensure(string candidateModelHash, int candidateDim)
    {
        if (!string.Equals(candidateModelHash, ModelHash, StringComparison.Ordinal))
        {
            throw new VectorIndexGuardMismatchException(
                $"Model hash mismatch: guard '{ModelHash}' != candidate '{candidateModelHash}'.");
        }

        if (candidateDim != Dim)
        {
            throw new VectorIndexGuardMismatchException(
                $"Vector dimension mismatch: guard dim {Dim} != candidate dim {candidateDim}.");
        }
    }
}