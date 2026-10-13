using System;

namespace ProvostXMemory.VectorIndex;

/// <summary>Guards model hash + vector dimension at every index mutation boundary.</summary>
public interface IVectorIndexGuard
{
    string ModelHash { get; }

    int Dim { get; }

    /// <summary>Throws <see cref="VectorIndexGuardMismatchException"/> on model-hash or dim mismatch.</summary>
    void Ensure(string candidateModelHash, int candidateDim);
}