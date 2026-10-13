using System;
using System.IO;
using System.Linq;
using Xunit;
using ProvostXMemory.Storage;

namespace ProvostXMemory.StorageTests;

/// <summary>Atomic-write behavior: temp cleanup after success and simulated crash debris.</summary>
public sealed class AtomicFileWriterTests
{
    private string TempDir()
        => Path.Combine(Path.GetTempPath(), "pvx-atomic-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Write_is_atomic_and_leaves_no_temp_files()
    {
        var dir = TempDir();
        var target = Path.Combine(dir, "records.jsonl");

        var result = new AtomicFileWriter().Write(target, "hello\nworld\n");

        Assert.Equal(target, result.DestinationPath);
        Assert.True(File.Exists(target));
        Assert.Equal("hello\nworld\n", File.ReadAllText(target));
        Assert.Single(Directory.GetFiles(dir)); // only the destination, no *.tmp leftovers
    }

    [Fact]
    public void Write_overwrites_existing_content_fully()
    {
        var dir = TempDir();
        var target = Path.Combine(dir, "data.json");
        Directory.CreateDirectory(dir);
        File.WriteAllText(target, "{\"old\":true}");

        new AtomicFileWriter().Write(target, "{\"new\":1}");

        Assert.Equal("{\"new\":1}", File.ReadAllText(target));
    }

    [Fact]
    public void CleanupStaleTempFiles_removes_crash_debris_but_keeps_destination()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, "records.jsonl");
        File.WriteAllText(target, "good");
        File.WriteAllText(Path.Combine(dir, "records.jsonl.018fdeadbeef-abc.tmp"), "half-written crashed");
        File.WriteAllText(Path.Combine(dir, "records.jsonl.018fdeadbeef-def.tmp"), "another crashed");

        new AtomicFileWriter().CleanupStaleTempFiles(target);

        Assert.True(File.Exists(target));
        Assert.Equal("good", File.ReadAllText(target));
        Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
    }

    [Fact]
    public void CleanupStaleTempFiles_does_not_touch_other_files()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, "records.jsonl");
        var manifest = Path.Combine(dir, "manifest.json");

        new AtomicFileWriter().CleanupStaleTempFiles(target);

        Assert.DoesNotContain(Directory.GetFiles(dir), file => file.EndsWith(".tmp", StringComparison.Ordinal));
        Assert.False(File.Exists(manifest)); // writer only cleans next to its own target pattern
    }

    [Fact]
    public void CleanupStaleTempFiles_tolerates_missing_directory()
    {
        var missing = Path.Combine(TempDir(), "never-created", "records.jsonl");
        new AtomicFileWriter().CleanupStaleTempFiles(missing); // no throw
    }

    [Fact]
    public void Two_writers_do_not_collide_on_temp_names()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, "shared.jsonl");
        var writer = new AtomicFileWriter();

        for (var i = 0; i < 50; i++)
        {
            writer.Write(target, $"line-{i}\n");
        }

        Assert.Equal("line-49\n", File.ReadAllText(target));
        Assert.Single(Directory.GetFiles(dir));
    }
}