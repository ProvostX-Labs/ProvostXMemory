# ProvostXMemory.Logging.API.md

Types: 12  |  LOC: 490  |  ~619 tokens

---

### Interface: IMemoryLogSink
> Destination for memory log entries.
Methods:
  - Task WriteAsync(MemoryLogEntry entry, CancellationToken cancellationToken = default)

### Class: ConsoleMemoryLogSink
> Writes log entries to standard output as plain text lines.
Implements: IMemoryLogSink

### Class: FileMemoryLogSink
> Append-only file sink with per-date rolling and lazy directory creation.
Implements: IMemoryLogSink
Constructor:
  - FileMemoryLogSink(string directory, string fileNamePattern, MemoryLogFormat format)

### Class: HashChainAppender
> Computes SHA-256 entry hashes chained to the previous entry's hash and
Constructor:
  - HashChainAppender(string lastHash, long lastSequence)

### Class: LogChainVerifier
> Pure verifier for hash-chained JSON-lines log files. Reads a file, checks

### Class: MemoryLogJson
> Wire shape of a log entry line used by the file sink and the chain verifier.

### Class: MemoryLogger
> Facade over the logging pipeline: on/off gate, level filter, hash chaining
Constructor:
  - MemoryLogger(MemoryLoggingOptions options, IMemoryLogSink? injectedSink = null, HashChainAppender? injectedChain = null)

### Class: RetentionSweeper
> Deletes log files older than the configured retention window.
Constructor:
  - RetentionSweeper(MemoryLoggingOptions options, Func<DateTimeOffset>? utcNow = null)

### Record: LogChainVerificationResult
> Result of verifying a hash-chained log file.
Constructor:
  - LogChainVerificationResult(bool IsValid, long? FirstBrokenLine, string? Reason)

### Record: MemoryLogEntry
> A single memory log entry.
Constructor:
  - MemoryLogEntry(DateTimeOffset TimestampUtc, long SequenceNumber, MemoryLogLevel Level, MemoryLogCategory Category, string Message, MemoryLogScope? Scope = null, string? CorrelationId = null, double? DurationMs = null, string? PreviousHash = null, string? EntryHash = null, string? ContentExcerpt = null)

### Record: MemoryLogScope
> Scope information attached to a log entry.
Constructor:
  - MemoryLogScope(string Kind, string Id)

### Record: MemoryLoggingOptions
> Configuration for memory logging.
Constructor:
  - MemoryLoggingOptions(bool Enabled = false, MemoryLogLevel Level = MemoryLogLevel.Info, MemoryLogSinkKind Sink = MemoryLogSinkKind.None, string Directory = "logs", string FileNamePattern = "memory-{date}.log", MemoryLogFormat Format = MemoryLogFormat.JsonLines, bool IncludeContent = false, MemoryLogIntegrity Integrity = MemoryLogIntegrity.None, int? RetentionDays = null)
