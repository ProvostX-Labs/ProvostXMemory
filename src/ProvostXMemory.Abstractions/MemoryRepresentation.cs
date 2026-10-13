using System.Collections.Generic;

namespace ProvostXMemory.Abstractions;

/// <summary>
/// <para>Additional per-modality content of a record: image captions, tables, audio
/// transcripts, or any parallel content that should be retrievable alongside the record's
/// main text. Each representation is embedded with the host's <c>IVectorEmbedder</c>
/// and indexed under its own vector id
/// (<c>repr|&lt;recordId&gt;|&lt;index&gt;</c>); the multi-representation retriever compares
/// a query against every representation and keeps the best-matching one per record.</para>
/// <para><b>Modality contract:</b> <see cref="Modality"/> is a tag, not a type system —
/// content is always string. <see cref="EmbeddingModelHash"/> is optional metadata for
/// hosts that run several embedding models; the vector index namespace guard keys on the
/// model hash + dimension, not on this field.</para>
/// </summary>
public sealed record MemoryRepresentation(
    MemoryRepresentationModality Modality,
    string Content,
    string? EmbeddingModelHash = null,
    IReadOnlyDictionary<string, string>? Properties = null)
{
    private static readonly IReadOnlyDictionary<string, string> EmptyProperties =
        new Dictionary<string, string>();

    public IReadOnlyDictionary<string, string> Properties { get; init; } = Properties ?? EmptyProperties;
}