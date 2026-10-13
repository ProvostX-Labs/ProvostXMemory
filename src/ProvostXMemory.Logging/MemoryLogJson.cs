using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProvostXMemory.Logging;

/// <summary>Wire shape of a log entry line used by the file sink and the chain verifier.</summary>
public sealed record MemoryLogJsonLine(
    [property: JsonPropertyName("ts")] string Timestamp,
    [property: JsonPropertyName("seq")] long SequenceNumber,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("cat")] string Category,
    [property: JsonPropertyName("msg")] string Message,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("corr")] string? CorrelationId,
    [property: JsonPropertyName("durMs")] double? DurationMs,
    [property: JsonPropertyName("prevHash")] string? PreviousHash,
    [property: JsonPropertyName("hash")] string? EntryHash,
    [property: JsonPropertyName("excerpt")] string? ContentExcerpt);

/// <summary>JSON (de)serialization helpers for log lines.</summary>
public static class MemoryLogJson
{
    // Stateless utility (pure): frozen serializer options, immutable after first use.
    /// <summary>Shared serializer options (stable property order, compact output).</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };

    /// <summary>Serializes an entry to one JSON line.</summary>
    public static string ToLine(MemoryLogEntry entry)
    {
        var line = new MemoryLogJsonLine(
            entry.TimestampUtc.ToString("O"),
            entry.SequenceNumber,
            entry.Level.ToString(),
            entry.Category.ToString(),
            entry.Message,
            entry.Scope is null ? null : entry.Scope.Kind + "|" + entry.Scope.Id,
            entry.CorrelationId,
            entry.DurationMs,
            entry.PreviousHash,
            entry.EntryHash,
            entry.ContentExcerpt);
        return JsonSerializer.Serialize(line, Options);
    }

    /// <summary>Parses one JSON line.</summary>
    public static MemoryLogJsonLine? FromLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        return JsonSerializer.Deserialize<MemoryLogJsonLine>(line, Options);
    }
}
