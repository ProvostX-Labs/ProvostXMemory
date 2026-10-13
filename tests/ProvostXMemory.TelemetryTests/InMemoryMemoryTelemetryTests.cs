using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Telemetry;
using Xunit;

namespace ProvostXMemory.TelemetryTests;

public sealed class InMemoryMemoryTelemetryTests
{
    [Fact]
    public async Task Reported_stages_are_captured_in_order()
    {
        var telemetry = new InMemoryMemoryTelemetry();

        await telemetry.ReportStageAsync("lexical", 1.0, new Dictionary<string, double> { ["bm25"] = 0.5 });
        await telemetry.ReportStageAsync("dense", 2.0, new Dictionary<string, double> { ["cosine"] = 0.8 });
        await telemetry.ReportStageAsync("fusion", 0.5, new Dictionary<string, double> { ["rrf"] = 0.6 });

        Assert.Equal(new[] { "lexical", "dense", "fusion" }, telemetry.StageNames);
        Assert.Equal(new[] { 1.0, 2.0, 0.5 }, telemetry.StageDurationsMs);
        Assert.Equal(0.5, telemetry.StageScores[0]["bm25"]);
        Assert.Equal(0.8, telemetry.StageScores[1]["cosine"]);
        Assert.Equal(0.6, telemetry.StageScores[2]["rrf"]);
    }

    [Fact]
    public async Task Backend_stats_are_captured_in_order()
    {
        var telemetry = new InMemoryMemoryTelemetry();

        await telemetry.ReportBackendStatsAsync(new BackendStats(Count: 1, BytesOnDisk: 10));
        await telemetry.ReportBackendStatsAsync(new BackendStats(Count: 2, BytesOnDisk: 20, LastFlushedAt: DateTimeOffset.UtcNow));

        Assert.Equal(2, telemetry.BackendStats.Count);
        Assert.Equal(1, telemetry.BackendStats[0].Count);
        Assert.Equal(2, telemetry.BackendStats[1].Count);
        Assert.Null(telemetry.BackendStats[0].LastFlushedAt);
        Assert.NotNull(telemetry.BackendStats[1].LastFlushedAt);
    }

    [Fact]
    public async Task Clear_drops_everything()
    {
        var telemetry = new InMemoryMemoryTelemetry();
        await telemetry.ReportStageAsync("lexical", 1.0, new Dictionary<string, double>());
        await telemetry.ReportBackendStatsAsync(new BackendStats(Count: 1, BytesOnDisk: 10));

        telemetry.Clear();

        Assert.Empty(telemetry.StageNames);
        Assert.Empty(telemetry.StageDurationsMs);
        Assert.Empty(telemetry.StageScores);
        Assert.Empty(telemetry.BackendStats);
    }

    [Fact]
    public async Task Captured_score_dictionaries_are_isolated_from_caller_mutation()
    {
        var telemetry = new InMemoryMemoryTelemetry();
        var scores = new Dictionary<string, double> { ["stage"] = 1.0 };

        await telemetry.ReportStageAsync("lexical", 1.0, scores);
        scores["stage"] = 99.0;

        Assert.Equal(1.0, telemetry.StageScores.Single()["stage"]);
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        var telemetry = new InMemoryMemoryTelemetry();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => telemetry.ReportStageAsync(null!, 1.0, new Dictionary<string, double>()));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => telemetry.ReportBackendStatsAsync(null!));
    }
}