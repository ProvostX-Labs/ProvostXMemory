using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Graph;

/// <summary>
/// Zero-dependency heuristic extractor: capitalized multi-word phrases and keyword/tag
/// matches become entities; entities co-occurring inside one record become relations
/// (label "co-occurs", weight = number of supporting records). Deliberately simple
/// default; hosts inject stronger <see cref="IEntityExtractor"/> implementations.
/// </summary>
public sealed class HeuristicEntityExtractor : IEntityExtractor
{
    private sealed record EntityAccumulator(GraphEntity Entity, HashSet<string> Records);

    private readonly IReadOnlyList<string> _keywords;

    /// <summary>Creates the extractor with an optional keyword list (matched case-insensitively as entities).</summary>
    public HeuristicEntityExtractor(IReadOnlyList<string>? keywords = null)
    {
        _keywords = keywords ?? Array.Empty<string>(); // pure fallback factory; no shared mutable state
    }

    public Task<ExtractionResult> ExtractAsync(IReadOnlyList<MemoryRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        cancellationToken.ThrowIfCancellationRequested();

        var keywordSet = new HashSet<string>(_keywords, StringComparer.OrdinalIgnoreCase);
        var entities = new Dictionary<string, EntityAccumulator>(StringComparer.Ordinal);
        var relations = new List<GraphRelation>();

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mentioned = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var phrase in CapitalizedPhrases(record.Content))
            {
                var entityId = ToEntityId(phrase);
                if (entityId.Length == 0)
                {
                    continue;
                }

                mentioned.Add(entityId);
                AddMention(entities, entityId, phrase, record.Id);
            }

            foreach (var tag in record.Tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                var entityId = ToEntityId(tag);
                if (entityId.Length == 0)
                {
                    continue;
                }

                mentioned.Add(entityId);
                AddMention(entities, entityId, tag, record.Id);
            }

            foreach (var keyword in keywordSet)
            {
                if (record.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    var entityId = ToEntityId(keyword);
                    mentioned.Add(entityId);
                    AddMention(entities, entityId, keyword, record.Id);
                }
            }

            if (mentioned.Count < 2)
            {
                continue;
            }

            // Every distinct pair of entities mentioned together in one record gets one relation instance;
            // the store deduplicates per unordered pair + label, so relation weight = number of supporting records.
            var ordered = mentioned.ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                for (var j = i + 1; j < ordered.Count; j++)
                {
                    relations.Add(new GraphRelation(
                        RelationId: ToRelationshipId(ordered[i], ordered[j]),
                        FromEntityId: ordered[i],
                        ToEntityId: ordered[j],
                        Label: "co-occurs",
                        RecordIds: new[] { record.Id }));
                }
            }
        }

        var entityList = entities.Values
            .Select(acc => new GraphEntity(
                acc.Entity.EntityId,
                acc.Entity.Name,
                acc.Entity.Aliases,
                acc.Records.OrderBy(id => id, StringComparer.Ordinal).ToList()))
            .OrderBy(e => e.EntityId, StringComparer.Ordinal)
            .ToList();

        var relationList = relations
            .GroupBy(r => r.RelationId, StringComparer.Ordinal)
            .Select(group => new GraphRelation(
                RelationId: group.Key,
                FromEntityId: group.First().FromEntityId,
                ToEntityId: group.First().ToEntityId,
                Label: group.First().Label,
                RecordIds: group
                    .SelectMany(r => r.RecordIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList()))
            .OrderBy(r => r.RelationId, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult(new ExtractionResult(entityList, relationList));
    }

    private void AddMention(Dictionary<string, EntityAccumulator> entities, string entityId, string displayName, string recordId)
    {
        if (!entities.TryGetValue(entityId, out var accumulator))
        {
            accumulator = new EntityAccumulator(
                new GraphEntity(entityId, displayName, Array.Empty<string>(), Array.Empty<string>()),
                new HashSet<string>(StringComparer.Ordinal));
            entities[entityId] = accumulator;
        }

        accumulator.Records.Add(recordId);
    }

    /// <summary>
    /// Extracts runs of adjacent capitalized words ("Alice Smith", "Frank Lloyd Wright") plus
    /// sentence-initial capitalized leads ("Frank went home." → "Frank"); single lowercase
    /// words and ALL-CAPS tokens are ignored.
    /// </summary>
    // Pure scanning function over one record's text; no shared mutable state.
    private static IEnumerable<string> CapitalizedPhrases(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            yield break;
        }

        var words = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var isSentenceStart = true;
        var index = 0;
        while (index < words.Length)
        {
            var word = words[index];
            if (!IsCapitalizedWord(word))
            {
                isSentenceStart = IsSentenceTerminatorAtEnd(word);
                index++;
                continue;
            }

            var isSentenceInitialLead = isSentenceStart
                && index + 1 < words.Length
                && !IsCapitalizedWord(words[index + 1]);
            var runEnd = index + 1;
            while (runEnd < words.Length && IsCapitalizedWord(words[runEnd]))
            {
                runEnd++;
            }

            var runLength = runEnd - index;
            if (runLength >= 2 || isSentenceInitialLead)
            {
                yield return string.Join(" ", words[index..runEnd]);
            }

            isSentenceStart = false;
            index = runEnd;
        }
    }

    // Pure predicate: capitalized token with at most one trailing punctuation mark; no shared mutable state.
    private static bool IsCapitalizedWord(string word)
    {
        if (word.Length == 0)
        {
            return false;
        }

        var letters = word.TrimEnd('!', '?', '.', ',', ':', ';');
        if (letters.Length == 0 || letters.Length >= word.Length && letters.Length == 1)
        {
            return false; // empty stem or single-letter token ("A", "I") is not an entity
        }

        if (!char.IsUpper(letters[0]))
        {
            return false;
        }

        for (var i = 1; i < letters.Length; i++)
        {
            if (!char.IsLetter(letters[i]))
            {
                return false;
            }
        }

        return true;
    }

    // Pure predicate; no shared mutable state.
    private static bool IsSentenceTerminatorAtEnd(string word)
    {
        return word.EndsWith('!') || word.EndsWith('?') || word.EndsWith('.');
    }

    // Pure slug function: "Frank Lloyd Wright 2" → "frank-lloyd-wright-2"; empty for id-less names.
    private static string ToEntityId(string name)
    {
        var builder = new StringBuilder();
        foreach (var ch in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        while (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length--;
        }

        return builder.ToString();
    }

    // Pure deterministic relation-slug factory for an unordered pair; no shared mutable state.
    private static string ToRelationshipId(string leftEntityId, string rightEntityId)
    {
        return string.CompareOrdinal(leftEntityId, rightEntityId) < 0
            ? $"{leftEntityId}+{rightEntityId}"
            : $"{rightEntityId}+{leftEntityId}";
    }
}