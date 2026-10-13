using System;
using System.Collections.Generic;

namespace ProvostXMemory.Lexical;

/// <summary>
/// Lowercases text and extracts contiguous [a0-9] word tokens (letters, digits, underscore).
/// Pure helpers (stateless string transforms); intentionally static per architecture rule.
/// </summary>
public static class LexicalTokenizer
{
    /// <summary>Tokenizes <paramref name="text"/> to lowercase word tokens ([letters, digits, underscore]+).</summary>
    public static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<string>();
        }

        var tokens = new List<string>();
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var isWordChar = i < text.Length && IsWordChar(text[i]);
            if (isWordChar)
            {
                if (start < 0)
                {
                    start = i;
                }
            }
            else if (start >= 0)
            {
                tokens.Add(text.Substring(start, i - start).ToLowerInvariant());
                start = -1;
            }
        }

        return tokens;
    }

    /// <summary>Counts token occurrences in <paramref name="text"/>.</summary>
    public static IReadOnlyDictionary<string, int> CountTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            var empty = new Dictionary<string, int>();
            return empty;
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var token in Tokenize(text))
        {
            counts[token] = counts.TryGetValue(token, out var existing) ? existing + 1 : 1;
        }

        return counts;
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }
}