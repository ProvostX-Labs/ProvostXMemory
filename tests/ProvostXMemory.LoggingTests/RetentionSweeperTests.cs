using ProvostXMemory.Abstractions;
using ProvostXMemory.Logging;
using Xunit;

namespace ProvostXMemory.LoggingTests;

public sealed class RetentionSweeperTests
{
    [Fact]
    public void Sweep_DeletesOnlyFilesOlderThanRetention()
    {
        var temp = Path.Combine(Path.GetTempPath(), "pvm-logging-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            var now = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
            WriteLog(temp, "memory-2026-03-15.log");
            WriteLog(temp, "memory-2026-03-09.log");
            WriteLog(temp, "memory-2026-03-08.log");
            WriteLog(temp, "memory-2026-02-20.log");
            WriteLog(temp, "memory-2026-02-01.log");
            WriteLog(temp, "unrelated.txt");

            var sweeper = new RetentionSweeper(
                new MemoryLoggingOptions(Enabled: true, Directory: temp, RetentionDays: 7),
                utcNow: () => now);

            var deleted = sweeper.Sweep();

            Assert.Equal(2, deleted);
            Assert.True(File.Exists(Path.Combine(temp, "memory-2026-03-15.log")));
            Assert.True(File.Exists(Path.Combine(temp, "memory-2026-03-09.log")));
            Assert.True(File.Exists(Path.Combine(temp, "memory-2026-03-08.log")));
            Assert.False(File.Exists(Path.Combine(temp, "memory-2026-02-20.log")));
            Assert.False(File.Exists(Path.Combine(temp, "memory-2026-02-01.log")));
            Assert.True(File.Exists(Path.Combine(temp, "unrelated.txt")));
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
    public void Sweep_WithoutRetentionWindowIsNoOp()
    {
        var temp = Path.Combine(Path.GetTempPath(), "pvm-logging-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            WriteLog(temp, "memory-2020-01-01.log");
            var sweeper = new RetentionSweeper(new MemoryLoggingOptions(Enabled: true, Directory: temp));

            Assert.Equal(0, sweeper.Sweep());
            Assert.True(File.Exists(Path.Combine(temp, "memory-2020-01-01.log")));
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
    public void TryParseFileDate_ExtractsDateFromPattern()
    {
        var date = RetentionSweeper.TryParseFileDate("memory-2026-05-04.log", "memory-{date}.log");
        Assert.Equal(new DateOnly(2026, 5, 4), date);
    }

    private static void WriteLog(string directory, string fileName) =>
        File.WriteAllText(Path.Combine(directory, fileName), "line\n");
}
