using ProvostXMemory.Abstractions;
using ProvostXMemory.Logging;
using Xunit;

namespace ProvostXMemory.LoggingTests;

public sealed class HashChainTests
{
    [Fact]
    public void Chain_VerifyPassesOnIntactLog()
    {
        var path = WriteChainedLog(3, _ => { });

        var result = LogChainVerifier.Verify(path);
        Assert.True(result.IsValid, result.Reason ?? "invalid");
        Assert.Null(result.FirstBrokenLine);
    }

    [Fact]
    public void Chain_ModifiedLineIsDetected()
    {
        var path = WriteChainedLog(4, lines =>
        {
            lines[2] = lines[2].Replace("\"msg\":\"m3\"", "\"msg\":\"TAMPERED\"", StringComparison.Ordinal);
        });

        var result = LogChainVerifier.Verify(path);
        Assert.False(result.IsValid);
        Assert.Equal(3, result.FirstBrokenLine);
    }

    [Fact]
    public void Chain_DeletedLineIsDetected()
    {
        var path = WriteChainedLog(4, lines => lines.RemoveAt(1));

        var result = LogChainVerifier.Verify(path);
        Assert.False(result.IsValid);
        Assert.Equal(2, result.FirstBrokenLine);
    }

    [Fact]
    public void Chain_SequenceGapIsDetected()
    {
        var path = WriteChainedLog(4, lines => lines.RemoveAt(2));

        var result = LogChainVerifier.Verify(path);
        Assert.False(result.IsValid);
        Assert.Equal(3, result.FirstBrokenLine);
        Assert.Contains("Sequence", result.Reason ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Chain_MonotonicSequenceNumbersAreAssigned()
    {
        var appender = new HashChainAppender();
        var first = appender.Append(BaseEntry("one"));
        var second = appender.Append(BaseEntry("two"));
        var third = appender.Append(BaseEntry("three"));

        Assert.Equal(1, first.SequenceNumber);
        Assert.Null(first.PreviousHash);
        Assert.Equal(2, second.SequenceNumber);
        Assert.Equal(first.EntryHash, second.PreviousHash);
        Assert.Equal(3, third.SequenceNumber);
        Assert.Equal(second.EntryHash, third.PreviousHash);
        Assert.NotEqual(first.EntryHash, second.EntryHash);
    }

    [Fact]
    public void Chain_ComputeEntryHashIsPure()
    {
        var a = HashChainAppender.ComputeEntryHash(1, "GENESIS", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), MemoryLogLevel.Info, MemoryLogCategory.Retrieval, "m", null, null, null, null);
        var b = HashChainAppender.ComputeEntryHash(1, "GENESIS", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), MemoryLogLevel.Info, MemoryLogCategory.Retrieval, "m", null, null, null, null);
        var c = HashChainAppender.ComputeEntryHash(2, "GENESIS", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), MemoryLogLevel.Info, MemoryLogCategory.Retrieval, "m", null, null, null, null);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public async Task Logger_ChainsEntriesThroughFileSink()
    {
        var temp = Path.Combine(Path.GetTempPath(), "pvm-logging-" + Guid.NewGuid().ToString("N"));
        try
        {
            var logger = new MemoryLogger(new MemoryLoggingOptions(
                Enabled: true,
                Sink: MemoryLogSinkKind.File,
                Directory: temp,
                Integrity: MemoryLogIntegrity.HashChain));

            logger.Log(MemoryLogLevel.Info, MemoryLogCategory.Lifecycle, "a");
            logger.Log(MemoryLogLevel.Info, MemoryLogCategory.Lifecycle, "b");
            logger.Log(MemoryLogLevel.Info, MemoryLogCategory.Lifecycle, "c");
            await Task.Delay(100);

            var file = Directory.EnumerateFiles(temp).Single();
            var result = LogChainVerifier.Verify(file);
            Assert.True(result.IsValid, result.Reason ?? "invalid");
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }

    private static string WriteChainedLog(int entryCount, Action<List<string>> mutate)
    {
        var appender = new HashChainAppender();
        var lines = new List<string>();
        var baseTimestamp = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 1; i <= entryCount; i++)
        {
            var stamped = appender.Append(new MemoryLogEntry(
                baseTimestamp.AddMinutes(i),
                0L,
                MemoryLogLevel.Info,
                MemoryLogCategory.Retrieval,
                $"m{i}"));
            lines.Add(MemoryLogJson.ToLine(stamped));
        }

        mutate(lines);
        var path = Path.Combine(Path.GetTempPath(), "pvm-chain-" + Guid.NewGuid().ToString("N") + ".log");
        File.WriteAllLines(path, lines);
        return path;
    }

    private static MemoryLogEntry BaseEntry(string message) =>
        new(DateTimeOffset.UtcNow, 0L, MemoryLogLevel.Info, MemoryLogCategory.Retrieval, message);

    /// <summary>
    /// Regression pin (2026-10-14): concurrent Log calls under hash-chain mode must produce
    /// a valid, ordered chain — before the fix, the unsynchronized appender plus
    /// fire-and-forget writes interleaved chain links and duplicated sequence numbers.
    /// </summary>
    [Fact]
    public void Chain_ConcurrentLoggingStaysValidAndOrdered()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pvm-log-conc-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = new MemoryLoggingOptions(
                Enabled: true,
                Level: MemoryLogLevel.Info,
                Sink: MemoryLogSinkKind.File,
                Directory: dir,
                FileNamePattern: "memory-{date}.log",
                Format: MemoryLogFormat.JsonLines,
                IncludeContent: false,
                Integrity: MemoryLogIntegrity.HashChain,
                RetentionDays: null);
            var logger = new MemoryLogger(options);

            var threads = new Thread[8];
            for (var t = 0; t < threads.Length; t++)
            {
                threads[t] = new Thread(() =>
                {
                    for (var i = 0; i < 25; i++)
                    {
                        logger.Log(MemoryLogLevel.Info, MemoryLogCategory.Lifecycle, "concurrent entry " + i);
                    }
                });
            }

            foreach (var thread in threads)
            {
                thread.Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            var file = Directory.GetFiles(dir, "memory-*.log").Single();
            var result = LogChainVerifier.Verify(file);
            Assert.True(result.IsValid, result.Reason ?? "invalid");
            var lines = File.ReadAllLines(file);
            Assert.Equal(200, lines.Length); // 8 threads x 25 entries, all serialized, none lost
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}