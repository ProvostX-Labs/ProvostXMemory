using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Logging;

namespace ProvostXMemoryTests;

/// <summary>Test double that records written log entries (thread-safe snapshot).</summary>
public sealed class FacadeRecordingLogSink : IMemoryLogSink
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
