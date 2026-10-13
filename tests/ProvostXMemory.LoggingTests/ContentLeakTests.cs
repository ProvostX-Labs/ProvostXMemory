using ProvostXMemory.Abstractions;
using ProvostXMemory.Logging;
using Xunit;

namespace ProvostXMemory.LoggingTests;

public sealed class ContentLeakTests
{
    private const string Secret = "CLASSIFIED-CONTENT-XYZ";

    [Fact]
    public async Task IncludeContentFalse_NeverWritesContentToJsonLines()
    {
        var temp = Path.Combine(Path.GetTempPath(), "pvm-logging-" + Guid.NewGuid().ToString("N"));
        try
        {
            var logger = new MemoryLogger(new MemoryLoggingOptions(
                Enabled: true,
                Sink: MemoryLogSinkKind.File,
                Directory: temp,
                Format: MemoryLogFormat.JsonLines,
                IncludeContent: false));

            logger.Log(MemoryLogLevel.Info, MemoryLogCategory.Retrieval, "retrieved", content: Secret);
            await Task.Delay(100);

            foreach (var file in Directory.EnumerateFiles(temp))
            {
                Assert.DoesNotContain(Secret, File.ReadAllText(file), StringComparison.Ordinal);
            }
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
    public async Task IncludeContentFalse_NeverWritesContentToPlainText()
    {
        var temp = Path.Combine(Path.GetTempPath(), "pvm-logging-" + Guid.NewGuid().ToString("N"));
        try
        {
            var logger = new MemoryLogger(new MemoryLoggingOptions(
                Enabled: true,
                Sink: MemoryLogSinkKind.File,
                Directory: temp,
                Format: MemoryLogFormat.PlainText,
                IncludeContent: false));

            logger.Log(MemoryLogLevel.Info, MemoryLogCategory.Consolidation, "consolidated", content: Secret);
            await Task.Delay(100);

            foreach (var file in Directory.EnumerateFiles(temp))
            {
                Assert.DoesNotContain(Secret, File.ReadAllText(file), StringComparison.Ordinal);
            }
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
    public async Task IncludeContentTrue_RecordsExcerpt()
    {
        var sink = new RecordingSink();
        var logger = new MemoryLogger(
            new MemoryLoggingOptions(
                Enabled: true,
                Sink: MemoryLogSinkKind.Injected,
                IncludeContent: true),
            injectedSink: sink);

        logger.Log(MemoryLogLevel.Info, MemoryLogCategory.Retrieval, "r", content: Secret);
        await WaitUntilAsync(() => sink.Entries.Count > 0);

        Assert.Equal(Secret, sink.Entries[0].ContentExcerpt);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }
    }
}
