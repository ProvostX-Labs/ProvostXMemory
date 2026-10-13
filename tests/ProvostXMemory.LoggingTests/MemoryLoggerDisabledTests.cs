using ProvostXMemory.Abstractions;
using ProvostXMemory.Logging;
using Xunit;

namespace ProvostXMemory.LoggingTests;

public sealed class MemoryLoggerDisabledTests
{
    [Fact]
    public async Task DisabledLogging_WritesNothingAndCreatesNoFile()
    {
        var temp = Path.Combine(Path.GetTempPath(), "pvm-logging-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = new MemoryLoggingOptions(
                Enabled: false,
                Sink: MemoryLogSinkKind.File,
                Directory: temp);
            var logger = new MemoryLogger(options);

            Assert.False(logger.IsEnabled);
            logger.Log(MemoryLogLevel.Error, MemoryLogCategory.StoreError, "boom", content: "secret-content");
            await Task.Delay(50);

            Assert.False(Directory.Exists(temp));
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DisabledLogging_CallsSinkZeroTimes()
    {
        var sink = new RecordingSink();
        var logger = new MemoryLogger(
            new MemoryLoggingOptions(Enabled: false, Sink: MemoryLogSinkKind.Injected),
            injectedSink: sink);

        logger.Log(MemoryLogLevel.Error, MemoryLogCategory.StoreError, "boom");

        await Task.Delay(50);
        Assert.Empty(sink.Entries);
    }

    [Fact]
    public async Task DisabledLogging_IsOptOutEvenForInjected()
    {
        var sink = new RecordingSink();
        var logger = new MemoryLogger(
            new MemoryLoggingOptions(Enabled: true, Sink: MemoryLogSinkKind.None),
            injectedSink: sink);

        logger.Log(MemoryLogLevel.Error, MemoryLogCategory.StoreError, "boom");

        await Task.Delay(50);
        Assert.Empty(sink.Entries);
    }
}
