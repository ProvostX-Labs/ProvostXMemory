using ProvostXMemory.Logging;

namespace ProvostXMemory.LoggingTests;

/// <summary>Test double that records written entries.</summary>
public sealed class RecordingSink : IMemoryLogSink
{
    private readonly object _lock = new();
    private readonly List<MemoryLogEntry> _entries = [];

    public IReadOnlyList<MemoryLogEntry> Entries
    {
        get { lock (_lock) { return _entries.ToArray(); } }
    }

    public Task WriteAsync(MemoryLogEntry entry, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _entries.Add(entry);
        }

        return Task.CompletedTask;
    }
}
