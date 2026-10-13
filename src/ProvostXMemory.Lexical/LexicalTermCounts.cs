using System;
using System.Collections.Generic;

namespace ProvostXMemory.Lexical;

/// <summary>
/// Term-frequency / token-set helpers over pre-tokenized term lists.
/// Pure functions over their inputs; intentionally static per architecture rule.
/// </summary>
public static class LexicalTermCounts
{
    /// <summary>Counts occurrences of each term in <paramref name="terms"/>.</summary>
    public static IReadOnlyDictionary<string, int> Count(IReadOnlyList<string> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var term in terms)
        {
            counts[term] = counts.TryGetValue(term, out var existing) ? existing + 1 : 1;
        }

        return counts;
    }

    /// <summary>Builds a distinct-term set from <paramref name="terms"/>.</summary>
    public static IReadOnlySet<string> Set(IReadOnlyList<string>? terms)
    {
        if (terms is null || terms.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var term in terms)
        {
            set.Add(term);
        }

        return set;
    }

    /// <summary>Merges two term-count dictionaries (right-hand side adds into left-hand side copy).</summary>
    public static IReadOnlyDictionary<string, int> Merge(
        IReadOnlyDictionary<string, int> first,
        IReadOnlyDictionary<string, int> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var merged = new Dictionary<string, int>(first, StringComparer.Ordinal);
        foreach (var (term, count) in second)
        {
            merged[term] = merged.TryGetValue(term, out var existing) ? existing + count : count;
        }

        return merged;
    }
}