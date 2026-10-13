namespace ProvostXMemory.Abstractions;

/// <summary>Severity of a memory log entry.</summary>
public enum MemoryLogLevel
{
    /// <summary>Detailed diagnostic information.</summary>
    Debug,
    /// <summary>Normal operational events.</summary>
    Info,
    /// <summary>Warnings that do not stop operation.</summary>
    Warn,
    /// <summary>Errors that prevented an operation.</summary>
    Error,
}
