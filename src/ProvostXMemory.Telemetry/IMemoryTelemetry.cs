using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Telemetry;

/// <summary>Query/consolidation timings, per-stage scores, backend stats (host-injected sink; default no-op).</summary>
public interface IMemoryTelemetry
{
    Task ReportStageAsync(string stageName, double durationMs, IReadOnlyDictionary<string, double> scores);

    Task ReportBackendStatsAsync(BackendStats stats);
}