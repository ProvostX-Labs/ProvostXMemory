using System.Globalization;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Logging;
using Xunit;

namespace ProvostXMemory.LoggingTests;

public sealed class FileRollingTests
{
    [Fact]
    public async Task FileSink_RollsAcrossTwoDates()
    {
        var temp = Path.Combine(Path.GetTempPath(), "pvm-logging-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            var sink = new FileMemoryLogSink(temp, "memory-{date}.log", MemoryLogFormat.JsonLines);
            var day1 = new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);
            var day2 = new DateTimeOffset(2026, 1, 11, 12, 0, 0, TimeSpan.Zero);
            await sink.WriteAsync(Entry(day1, "first day"));
            await sink.WriteAsync(Entry(day2, "second day"));
            await sink.WriteAsync(Entry(day2, "second day again"));

            Assert.True(File.Exists(Path.Combine(temp, "memory-2026-01-10.log")));
            Assert.True(File.Exists(Path.Combine(temp, "memory-2026-01-11.log")));
            var lineCount1 = File.ReadAllLines(Path.Combine(temp, "memory-2026-01-10.log")).Length;
            var lineCount2 = File.ReadAllLines(Path.Combine(temp, "memory-2026-01-11.log")).Length;
            Assert.Equal(1, lineCount1);
            Assert.Equal(2, lineCount2);
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
    public async Task FileSink_CreatesDirectoryLazily()
    {
        var temp = Path.Combine(Path.GetTempPath(), "pvm-logging-" + Guid.NewGuid().ToString("N"), "nested");
        try
        {
            var sink = new FileMemoryLogSink(temp, "memory-{date}.log", MemoryLogFormat.JsonLines);
            await sink.WriteAsync(Entry(DateTimeOffset.UtcNow, "lazy"));
            Assert.True(Directory.Exists(temp));
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }

    private static MemoryLogEntry Entry(DateTimeOffset timestamp, string message) =>
        new(timestamp, 0L, MemoryLogLevel.Info, MemoryLogCategory.Lifecycle, message);
}
