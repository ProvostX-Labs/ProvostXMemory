namespace ProvostXMemory.Logging;

/// <summary>Result of verifying a hash-chained log file.</summary>
/// <param name="IsValid">True when the chain and sequence numbers are intact.</param>
/// <param name="FirstBrokenLine">1-based line number of the first break, or null when valid.</param>
/// <param name="Reason">Human-readable description of the first break.</param>
public sealed record LogChainVerificationResult(bool IsValid, long? FirstBrokenLine, string? Reason);
