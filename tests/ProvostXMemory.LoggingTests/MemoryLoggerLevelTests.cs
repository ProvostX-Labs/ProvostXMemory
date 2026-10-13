using ProvostXMemory.Abstractions;
using ProvostXMemory.Logging;
using Xunit;

namespace ProvostXMemory.LoggingTests;

public sealed class MemoryLoggerLevelTests
{
    [Fact]
    public async Task LevelFilter_DropsEntriesBelowMinimum()
    {
        var sink = new RecordingSink();
        var logger = new MemoryLogger(
            new MemoryLoggingOptions(
                Enabled: true,
                Level: MemoryLogLevel.Warn,
                Sink: MemoryLogSinkKind.Injected),
            injectedSink: sink);

        logger.Log(MemoryLogLevel.Debug, MemoryLogCategory.Retrieval, "d");
        logger.Log(MemoryLogLevel.Info, MemoryLogCategory.Retrieval, "i");
        logger.Log(MemoryLogLevel.Warn, MemoryLogCategory.Retrieval, "w");
        logger.Log(MemoryLogLevel.Error, MemoryLogCategory.StoreError, "e");

        await WaitForAsync(() => sink.Entries.Count >= 2);
        Assert.Equal(2, sink.Entries.Count);
        Assert.Equal(MemoryLogLevel.Warn, sink.Entries[0].Level);
        Assert.Equal(MemoryLogLevel.Error, sink.Entries[1].Level);
    }

    [Fact]
    public async Task LevelFilter_DebugLevelPassesEverything()
    {
        var sink = new RecordingSink();
        var logger = new MemoryLogger(
            new MemoryLoggingOptions(
                Enabled: true,
                Level: MemoryLogLevel.Debug,
                Sink: MemoryLogSinkKind.Injected),
            injectedSink: sink);

        foreach (MemoryLogLevel level in Enum.GetValues<MemoryLogLevel>())
        {
            logger.Log(level, MemoryLogCategory.Lifecycle, "x");
        }

        await WaitForAsync(() => sink.Entries.Count >= 4);
        Assert.Equal(4, sink.Entries.Count);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }
    }
}
