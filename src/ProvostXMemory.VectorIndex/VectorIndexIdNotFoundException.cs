using System;

namespace ProvostXMemory.VectorIndex;

/// <summary>Remove of an id that is not present in the index.</summary>
public sealed class VectorIndexIdNotFoundException : Exception
{
    public VectorIndexIdNotFoundException(string id)
        : base($"Vector index id not found: '{id}'.")
    {
        Id = id;
    }

    public string Id { get; }
}