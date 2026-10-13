using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Telemetry;
using Xunit;

namespace ProvostXMemory.TelemetryTests;

public sealed class NoopMemoryTelemetryTests
{
    [Fact]
    public async Task ReportStage_completes_without_effect()
    {
        IMemoryTelemetry telemetry = NoopMemoryTelemetry.Instance;

        await telemetry.ReportStageAsync("lexical", 1.5, new Dictionary<string, double> { ["bm25"] = 0.4 });
        await telemetry.ReportStageAsync("dense", 2.5, new Dictionary<string, double> { ["cosine"] = 0.9 });
    }

    [Fact]
    public async Task ReportBackendStats_completes_without_effect()
    {
        IMemoryTelemetry telemetry = NoopMemoryTelemetry.Instance;

        await telemetry.ReportBackendStatsAsync(new BackendStats(Count: 5, BytesOnDisk: 1024));
    }

    [Fact]
    public void Instance_is_singleton()
    {
        Assert.Same(NoopMemoryTelemetry.Instance, NoopMemoryTelemetry.Instance);
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        IMemoryTelemetry telemetry = NoopMemoryTelemetry.Instance;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => telemetry.ReportStageAsync(null!, 1.0, new Dictionary<string, double>()));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => telemetry.ReportStageAsync("stage", 1.0, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => telemetry.ReportBackendStatsAsync(null!));
    }
}