using System;
using System.Collections.Generic;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Lexical;


/// <summary>A tokenized document with optional key terms and category.</summary>
public sealed record LexicalDocument(
    string Id,
    IReadOnlyList<string> BodyTerms,
    IReadOnlyList<string>? KeyTerms = null,
    string? Category = null)
{
    private static readonly IReadOnlyList<string> EmptyTerms = Array.Empty<string>();

    public IReadOnlyList<string> BodyTerms { get; init; } = BodyTerms ?? EmptyTerms;

    public IReadOnlyList<string> KeyTerms { get; init; } = KeyTerms ?? EmptyTerms;
}

