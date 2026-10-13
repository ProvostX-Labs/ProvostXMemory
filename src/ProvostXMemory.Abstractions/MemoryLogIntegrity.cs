namespace ProvostXMemory.Abstractions;

/// <summary>Integrity protection applied to log entries.</summary>
public enum MemoryLogIntegrity
{
    /// <summary>No integrity protection.</summary>
    None,
    /// <summary>SHA-256 hash chain with monotonic sequence numbers.</summary>
    HashChain,
}
