using System.Security.Cryptography;
using System.Text;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Logging;

/// <summary>
/// Computes SHA-256 entry hashes chained to the previous entry's hash and
/// assigns monotonic sequence numbers. Pure computation is static; the
/// instance tracks only the chain head.
/// </summary>
public sealed class HashChainAppender
{
    private readonly string _genesisHash;
    private long _lastSequence;
    private string _lastHash;

    /// <summary>Creates an appender starting from the genesis (empty) chain.</summary>
    public HashChainAppender()
        : this("GENESIS", 0L)
    {
    }

    /// <summary>Creates an appender resuming from a known chain head.</summary>
    /// <param name="lastHash">Hash of the most recent entry, or a genesis marker.</param>
    /// <param name="lastSequence">Sequence number of the most recent entry.</param>
    public HashChainAppender(string lastHash, long lastSequence)
    {
        _lastHash = lastHash;
        _lastSequence = lastSequence;
        _genesisHash = "GENESIS";
    }

    /// <summary>Gets the hash of the most recently appended entry.</summary>
    public string LastHash => _lastHash;

    /// <summary>Gets the sequence number of the most recently appended entry.</summary>
    public long LastSequence => _lastSequence;

    // Stateless utility (pure): hash over canonical entry bytes only, no state.
    /// <summary>
    /// Pure hash computation over a canonical representation of an entry.
    /// </summary>
    public static string ComputeEntryHash(
        long sequenceNumber,
        string previousHash,
        DateTimeOffset timestampUtc,
        MemoryLogLevel level,
        MemoryLogCategory category,
        string message,
        MemoryLogScope? scope,
        string? correlationId,
        double? durationMs,
        string? contentExcerpt)
    {
        var canonical = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{sequenceNumber}|{previousHash}|{timestampUtc:O}|{level}|{category}|{message}|{scope?.Kind ?? "-"}|{scope?.Id ?? "-"}|{correlationId ?? "-"}|{durationMs?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}|{contentExcerpt ?? "-"}");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }

    /// <summary>Stamps an entry with the next sequence number and chained hashes.</summary>
    public MemoryLogEntry Append(MemoryLogEntry entry)
    {
        var nextSequence = _lastSequence + 1;
        var previousHash = _lastHash == _genesisHash ? null : _lastHash;
        var hash = ComputeEntryHash(
            nextSequence,
            previousHash ?? _genesisHash,
            entry.TimestampUtc,
            entry.Level,
            entry.Category,
            entry.Message,
            entry.Scope,
            entry.CorrelationId,
            entry.DurationMs,
            entry.ContentExcerpt);
        _lastSequence = nextSequence;
        _lastHash = hash;
        return entry with
        {
            SequenceNumber = nextSequence,
            PreviousHash = previousHash,
            EntryHash = hash,
        };
    }
}
