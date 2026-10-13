namespace ProvostXMemory.Abstractions;

/// <summary>Seriline format used by the file sink.</summary>
public enum MemoryLogFormat
{
    /// <summary>One JSON object per line.</summary>
    JsonLines,
    /// <summary>Human-readable text lines.</summary>
    PlainText,
}
