using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Logging;

// Stateless utility (pure): reads input, returns a result, mutates nothing.
/// <summary>
/// Pure verifier for hash-chained JSON-lines log files. Reads a file, checks
/// hash-chain linkage and sequence-number continuity, and reports the first
/// broken line when the log has been tampered with.
/// </summary>
public static class LogChainVerifier
{
    /// <summary>Verifies a log file written in JsonLines format with hash-chain integrity.</summary>
    /// <param name="filePath">Path to the log file.</param>
    public static LogChainVerificationResult Verify(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new LogChainVerificationResult(false, null, "Log file does not exist.");
        }

        var lines = File.ReadAllLines(filePath);
        long expectedSequence = 1L;
        string? previousHash = null;
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                return new LogChainVerificationResult(false, lineNumber, "Empty line in log file.");
            }

            MemoryLogJsonLine? parsed;
            try
            {
                parsed = MemoryLogJson.FromLine(lines[i]);
            }
            catch (Exception)
            {
                return new LogChainVerificationResult(false, lineNumber, "Line is not valid JSON.");
            }

            if (parsed is null)
            {
                return new LogChainVerificationResult(false, lineNumber, "Line could not be parsed.");
            }

            if (parsed.EntryHash is null)
            {
                return new LogChainVerificationResult(false, lineNumber, "Line has no entry hash.");
            }

            if (parsed.SequenceNumber != expectedSequence)
            {
                return new LogChainVerificationResult(
                    false,
                    lineNumber,
                    $"Sequence gap or ordering violation: expected {expectedSequence}, found {parsed.SequenceNumber}.");
            }

            var scope = ParseScope(parsed.Scope);
            var recomputed = HashChainAppender.ComputeEntryHash(
                parsed.SequenceNumber,
                previousHash ?? "GENESIS",
                DateTimeOffset.Parse(parsed.Timestamp),
                Enum.Parse<MemoryLogLevel>(parsed.Level, ignoreCase: false),
                Enum.Parse<MemoryLogCategory>(parsed.Category, ignoreCase: false),
                parsed.Message,
                scope,
                parsed.CorrelationId,
                parsed.DurationMs,
                parsed.ContentExcerpt);
            if (!string.Equals(recomputed, parsed.EntryHash, StringComparison.OrdinalIgnoreCase))
            {
                return new LogChainVerificationResult(false, lineNumber, "Entry hash mismatch (content was modified).");
            }

            if (previousHash is not null && !string.Equals(parsed.PreviousHash, previousHash, StringComparison.OrdinalIgnoreCase))
            {
                return new LogChainVerificationResult(false, lineNumber, "Chain link mismatch (entry was removed or reordered).");
            }

            previousHash = parsed.EntryHash;
            expectedSequence++;
        }

        return new LogChainVerificationResult(true, null, null);
    }

    private static MemoryLogScope? ParseScope(string? scope)
    {
        if (scope is null)
        {
            return null;
        }

        var separator = scope.IndexOf('|', StringComparison.Ordinal);
        return separator < 0
            ? new MemoryLogScope(scope, string.Empty)
            : new MemoryLogScope(scope[..separator], scope[(separator + 1)..]);
    }
}
