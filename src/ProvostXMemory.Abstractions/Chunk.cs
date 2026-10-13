using System.Collections.Generic;

namespace ProvostXMemory.Abstractions;

/// <summary>
/// A chunk produced by splitting a memory record. <see cref="StartOffset"/> and
/// <see cref="EndOffset"/> are half-open offsets into the parent record's content
/// (<c>parentContent[StartOffset..EndOffset] == Content</c>); the value -1 means "unset".
/// Chunkers always set both.
/// </summary>
public sealed record Chunk(
    string ChunkId,
    string RecordId,
    string Content,
    int Index,
    IReadOnlyList<string>? Tags = null,
    int StartOffset = -1,
    int EndOffset = -1)
{
    private static readonly IReadOnlyList<string> EmptyTags = new string[0];

    public IReadOnlyList<string> Tags { get; init; } = Tags ?? EmptyTags;
}