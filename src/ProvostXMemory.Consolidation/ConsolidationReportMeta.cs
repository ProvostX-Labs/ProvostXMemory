using System.Text.Json;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Consolidation;

/// <summary>
/// Side channel that attaches the pass <see cref="ConsolidationReport"/> to the records
/// returned by <see cref="IMemoryConsolidator"/>: the interface signature is fixed by the
/// scaffold, so the report travels as a "consolidation-report" Meta entry on each returned
/// record (returned copies only — store contents are never touched by this). Pure
/// helpers over immutable records; the serializer options are immutable after creation.
/// </summary>
public static class ConsolidationReportMeta
{
    /// <summary>Meta key under which the report is attached to returned records.</summary>
    public const string MetaKey = "consolidation-report";

    // Pure factory of immutable serializer options (camelCase, matching the store codec style).
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    /// <summary>Attaches the report to every returned record's Meta (rebuilt copies).</summary>
    public static IReadOnlyList<MemoryRecord> Apply(IReadOnlyList<MemoryRecord> records, ConsolidationReport report)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(report);

        var json = JsonSerializer.Serialize(report, JsonOptions);
        var merged = new List<MemoryRecord>(records.Count);
        foreach (var record in records)
        {
            var meta = new Dictionary<string, string>(record.Meta, StringComparer.Ordinal)
            {
                [MetaKey] = json,
            };
            merged.Add(record with { Meta = meta });
        }

        return merged;
    }

    /// <summary>Reads the report attached by <see cref="Apply"/> from any returned record.</summary>
    public static ConsolidationReport? TryGet(MemoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Meta.TryGetValue(MetaKey, out var json)
            ? JsonSerializer.Deserialize<ConsolidationReport>(json, JsonOptions)
            : null;
    }

    // Pure factory: immutable serializer options.
    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }
}