using System;
using System.Collections.Generic;

namespace ProvostXMemory.Abstractions;

/// <summary>Immutable tag/category/date filter set for retrieval.</summary>
public sealed record RetrievalFilters(
    IReadOnlyList<string>? Tags = null,
    string? Category = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null)
{
    private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();

    public IReadOnlyList<string> Tags { get; init; } = Tags ?? EmptyTags;
}