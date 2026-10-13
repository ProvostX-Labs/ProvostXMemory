using System;
using System.Collections.Generic;

namespace ProvostXMemory.Abstractions;

/// <summary>
/// <para>The unit of remembered knowledge: an immutable record with identity, scope, content,
/// and retrieval hints. Records are created by hosts and persisted through
/// <c>IMemoryService.RememberAsync</c>; retrieval returns them wrapped in
/// <c>ScoredMemoryRecord</c>.</para>
/// <para>Retrieval relevance is driven by <see cref="Content"/> (embedded and lexically
/// indexed), boosted by <see cref="Tags"/> and <see cref="Key"/> terms and
/// <see cref="Category"/> exact matches in the default reranker. Optional
/// <see cref="Reps"/> add per-modality content (image captions, tables) that is embedded
/// alongside the text and retrievable through the multi-representation retriever.</para>
/// </summary>
/// <remarks>
/// <para>Lifecycle: <c>CreatedAt</c> and <c>UpdatedAt</c> default to UTC now; consolidation
/// keeps the OLDEST record of each duplicate group (so timestamps determine dedup winners);
/// <c>ExpiresAt</c> in the past marks a record for TTL deletion on the next consolidation pass;
/// <c>Meta</c> is a free-form bag persisted with the record (used e.g. for cluster-summary flags).
/// Ids must be unique per scope and should not contain '|', '-', or path separators (the
/// id-separator restriction keeps vector/chunk ids reversible; violators are rejected).</para>
/// </remarks>
public sealed record MemoryRecord(
    string Id,
    MemoryScope Scope,
    string Key,
    string Content,
    string? Category = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<MemoryRepresentation>? Reps = null)
{
    private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();
    private static readonly IReadOnlyList<MemoryRepresentation> EmptyReps =
        Array.Empty<MemoryRepresentation>();
    private static readonly IReadOnlyDictionary<string, string> EmptyMeta =
        new Dictionary<string, string>();

    public IReadOnlyList<string> Tags { get; init; } = Tags ?? EmptyTags;

    public IReadOnlyList<MemoryRepresentation> Reps { get; init; } = Reps ?? EmptyReps;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ExpiresAt { get; init; }

    public IReadOnlyDictionary<string, string> Meta { get; init; } = EmptyMeta;
}