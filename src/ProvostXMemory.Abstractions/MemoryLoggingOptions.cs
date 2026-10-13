namespace ProvostXMemory.Abstractions;

/// <summary>
/// <para>Logging configuration for the memory service. Off by default —
/// <c>Enabled = false</c> means zero writes, zero log-entry allocations on the hot path,
/// no log directory created. Enabled logging is content-minimized: log lines carry
/// what happened (scope, operation, scores, durations), never record content itself,
/// unless <c>IncludeContent</c> is explicitly set — the default-persisted posture is
/// behavior-evidence without user-content retention.</para>
/// <para><b>Audit mode:</b> <c>Integrity = MemoryLogIntegrity.HashChain</c> appends entries
/// SHA-256 hash-chained to their predecessor (with monotonic sequence numbers) under a
/// serialized gate — any modified or deleted line breaks verification, and
/// <c>LogChainVerifier</c> proves integrity in one pass. <c>RetentionDays</c> triggers the
/// retention sweeper; both file and console sinks are available, hosts inject their own
/// <c>IMemoryLogSink</c> (SIEM, Serilog, …) through the builder.</para>
/// </summary>
/// <param name="Enabled">Master toggle; disabled logging performs no writes and allocates no entries.</param>
/// <param name="Level">Minimum level that is written.</param>
/// <param name="Sink">Where entries go.</param>
/// <param name="Directory">Log directory; consumers typically resolve relative to the memory root.</param>
/// <param name="FileNamePattern">File name pattern; <c>{date}</c> is replaced by the entry date.</param>
/// <param name="Format">Line format used by the file sink.</param>
/// <param name="IncludeContent">When false, content is never present in any log output.</param>
/// <param name="Integrity">Integrity protection applied to written entries.</param>
/// <param name="RetentionDays">Optional retention window; older log files are deleted by the sweeper.</param>
public sealed record MemoryLoggingOptions(
    bool Enabled = false,
    MemoryLogLevel Level = MemoryLogLevel.Info,
    MemoryLogSinkKind Sink = MemoryLogSinkKind.None,
    string Directory = "logs",
    string FileNamePattern = "memory-{date}.log",
    MemoryLogFormat Format = MemoryLogFormat.JsonLines,
    bool IncludeContent = false,
    MemoryLogIntegrity Integrity = MemoryLogIntegrity.None,
    int? RetentionDays = null);
