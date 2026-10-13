using System.Collections.Generic;

namespace ProvostXMemory.Abstractions;

/// <summary>
/// Logical record model as persisted on disk (JSONL entry shape). <see cref="Reps"/> and
/// <see cref="Meta"/> are optional trailing parameters: entries written before they existed
/// (or writers that leave them out) deserialize with null, which the store maps to the
/// record's empty defaults — never a null crash.
/// </summary>
public sealed record MemoryEntryModel(
    string Id,
    string ScopeKind,
    string ScopeId,
    string Key,
    string Content,
    string? Category,
    IReadOnlyList<string> Tags,
    long CreatedAtUnixMs,
    long UpdatedAtUnixMs,
    long? ExpiresAtUnixMs,
    IReadOnlyList<MemoryRepresentation>? Reps = null,
    IReadOnlyDictionary<string, string>? Meta = null);