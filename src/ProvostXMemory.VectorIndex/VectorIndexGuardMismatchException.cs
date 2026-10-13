using System;

namespace ProvostXMemory.VectorIndex;

/// <summary>Embedder model hash or vector dimension does not match the index guard.</summary>
public sealed class VectorIndexGuardMismatchException : Exception
{
    public VectorIndexGuardMismatchException(string message)
        : base(message)
    {
    }
}