using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Logging;

/// <summary>A single memory log entry.</summary>
/// <param name="TimestampUtc">UTC timestamp of the event.</param>
/// <param name="SequenceNumber">Monotonic sequence number (only with hash-chain integrity).</param>
/// <param name="Level">Severity.</param>
/// <param name="Category">Functional area.</param>
/// <param name="Message">Human-readable message (never contains content unless IncludeContent).</param>
/// <param name="Scope">Optional scope information.</param>
/// <param name="CorrelationId">Optional correlation identifier.</param>
/// <param name="DurationMs">Optional operation duration in milliseconds.</param>
/// <param name="PreviousHash">Hash of the previous chained entry.</param>
/// <param name="EntryHash">Hash of this entry.</param>
/// <param name="ContentExcerpt">Optional content excerpt; null unless explicitly enabled.</param>
public sealed record MemoryLogEntry(
    DateTimeOffset TimestampUtc,
    long SequenceNumber,
    MemoryLogLevel Level,
    MemoryLogCategory Category,
    string Message,
    MemoryLogScope? Scope = null,
    string? CorrelationId = null,
    double? DurationMs = null,
    string? PreviousHash = null,
    string? EntryHash = null,
    string? ContentExcerpt = null);
