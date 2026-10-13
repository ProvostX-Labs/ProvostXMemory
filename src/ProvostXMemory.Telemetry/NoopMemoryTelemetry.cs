using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Telemetry;

/// <summary>Zero-op default telemetry sink. Never throws, never allocates meaningfully.</summary>
public sealed class NoopMemoryTelemetry : IMemoryTelemetry
{
    // Immutable, stateless sink: safe to share as a process-wide default instance.
    private static readonly NoopMemoryTelemetry SharedInstance = new();

    private NoopMemoryTelemetry()
    {
    }

    /// <summary>Shared default instance (immutable, stateless).</summary>
    public static NoopMemoryTelemetry Instance => SharedInstance;

    public Task ReportStageAsync(string stageName, double durationMs, IReadOnlyDictionary<string, double> scores)
    {
        ArgumentNullException.ThrowIfNull(stageName);
        ArgumentNullException.ThrowIfNull(scores);
        return Task.CompletedTask;
    }

    public Task ReportBackendStatsAsync(BackendStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        return Task.CompletedTask;
    }
}