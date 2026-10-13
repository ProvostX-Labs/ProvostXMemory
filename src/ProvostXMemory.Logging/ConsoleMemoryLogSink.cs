using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Logging;

/// <summary>Writes log entries to standard output as plain text lines.</summary>
public sealed class ConsoleMemoryLogSink : IMemoryLogSink
{
    /// <inheritdoc />
    public Task WriteAsync(MemoryLogEntry entry, CancellationToken cancellationToken = default)
    {
        Console.WriteLine(FileMemoryLogSink.FormatLine(entry, MemoryLogFormat.PlainText));
        return Task.CompletedTask;
    }
}
