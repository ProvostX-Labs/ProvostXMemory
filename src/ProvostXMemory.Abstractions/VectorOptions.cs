namespace ProvostXMemory.Abstractions;

/// <summary>Vector index options: backend kind, model hash, dimension, default top-k.</summary>
public sealed record VectorOptions(
    string EmbeddingModelHash,
    int Dim,
    IndexKind IndexKind = IndexKind.Flat,
    int DefaultTopK = 10);
