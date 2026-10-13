using System;
using System.Collections.Generic;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Fusion;


/// <summary>A single ranked list input to fusion: source name plus ranked hits.</summary>
public sealed record RankedList(string Source, IReadOnlyList<ScoredId> Items)
{
    private static readonly IReadOnlyList<ScoredId> EmptyItems = Array.Empty<ScoredId>();

    public IReadOnlyList<ScoredId> Items { get; init; } = Items ?? EmptyItems;
}

