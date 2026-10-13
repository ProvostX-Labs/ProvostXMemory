namespace ProvostXMemory.Abstractions;

/// <summary>Where log entries are written.</summary>
public enum MemoryLogSinkKind
{
    /// <summary>No sink; entries are dropped.</summary>
    None,
    /// <summary>Rolling file sink.</summary>
    File,
    /// <summary>Console sink.</summary>
    Console,
    /// <summary>Caller-supplied sink.</summary>
    Injected,
}
