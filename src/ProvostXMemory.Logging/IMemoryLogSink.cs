namespace ProvostXMemory.Logging;

/// <summary>Destination for memory log entries.</summary>
public interface IMemoryLogSink
{
    /// <summary>Writes one entry.</summary>
    Task WriteAsync(MemoryLogEntry entry, CancellationToken cancellationToken = default);
}
