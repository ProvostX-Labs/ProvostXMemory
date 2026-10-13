using System;
using System.Collections.Generic;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Retrieval;

/// <summary>
/// Pure predicate that enforces the <see cref="RetrievalFilters"/> carried on a
/// <see cref="RetrievalQuery"/> against resolved records: category exact-match,
/// tag any-match, and date window (From/To) on the record's <see cref="MemoryRecord.CreatedAt"/>.
/// A null filter set matches everything, so unfiltered retrieval is behaviorally unchanged.
/// Intentionally static: it is a pure function per the architecture rule.
/// </summary>
public static class RetrievalFilterApplier
{
    /// <summary>Returns true when the record satisfies every active filter dimension.</summary>
    public static bool Matches(MemoryRecord record, RetrievalFilters? filters)
    {
        if (filters is null)
        {
            return true;
        }

        if (filters.Category is { } category
            && !string.Equals(record.Category, category, StringComparison.Ordinal))
        {
            return false;
        }

        if (filters.Tags.Count > 0)
        {
            var matched = false;
            foreach (var tag in record.Tags)
            {
                foreach (var wanted in filters.Tags)
                {
                    if (string.Equals(tag, wanted, StringComparison.Ordinal))
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    break;
                }
            }

            if (!matched)
            {
                return false;
            }
        }

        if (filters.From is { } from && record.CreatedAt < from)
        {
            return false;
        }

        if (filters.To is { } to && record.CreatedAt > to)
        {
            return false;
        }

        return true;
    }

    /// <summary>Returns only the scored records that match the filter set (order preserved).</summary>
    public static IReadOnlyList<ScoredMemoryRecord> Apply(
        IReadOnlyList<ScoredMemoryRecord> records,
        RetrievalFilters? filters)
    {
        if (filters is null)
        {
            return records;
        }

        var kept = new List<ScoredMemoryRecord>(records.Count);
        foreach (var scored in records)
        {
            if (Matches(scored.Record, filters))
            {
                kept.Add(scored);
            }
        }

        return kept;
    }
}
