using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Storage;
using Xunit;

namespace ProvostXMemory.StorageTests;

public sealed class MemoryManifestCodecTests : IDisposable
{
    private readonly string _root;

    public MemoryManifestCodecTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "pvx-manifest-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Read_missing_file_returns_empty_manifest()
    {
        var codec = new MemoryManifestCodec();
        var manifest = await codec.ReadAsync(_root);

        Assert.Equal("0", manifest.Version);
        Assert.Equal(0, manifest.EntryCount);
        Assert.Equal(0, manifest.UpdatedAtUnixMs);
    }

    [Fact]
    public async Task Round_trip_preserves_all_fields()
    {
        var codec = new MemoryManifestCodec();
        var original = new MemoryManifest(Version: "1.2.0", EntryCount: 42, UpdatedAtUnixMs: 1_700_000_000_000);

        await codec.WriteAsync(_root, original);
        var read = await codec.ReadAsync(_root);

        Assert.Equal(original, read);
    }

    [Fact]
    public async Task Overwrite_replaces_previous_manifest()
    {
        var codec = new MemoryManifestCodec();
        await codec.WriteAsync(_root, new MemoryManifest("1", 10, 100));
        await codec.WriteAsync(_root, new MemoryManifest("2", 20, 200));

        var read = await codec.ReadAsync(_root);
        Assert.Equal(new MemoryManifest("2", 20, 200), read);
    }

    [Fact]
    public async Task Manifest_is_written_as_camel_case_json_at_expected_path()
    {
        var codec = new MemoryManifestCodec();
        await codec.WriteAsync(_root, new MemoryManifest("3", 7, 300));

        var path = Path.Combine(_root, "manifest.json");
        Assert.True(File.Exists(path));
        var json = await File.ReadAllTextAsync(path);
        Assert.Contains("\"version\"", json);
        Assert.Contains("\"entryCount\"", json);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(7, doc.RootElement.GetProperty("entryCount").GetInt64());
    }

    [Fact]
    public async Task Empty_manifest_file_reads_as_empty()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "manifest.json"), string.Empty);
        var codec = new MemoryManifestCodec();

        var manifest = await codec.ReadAsync(_root);

        Assert.Equal(0, manifest.EntryCount);
        Assert.Equal("0", manifest.Version);
    }

    [Fact]
    public async Task Corrupt_json_reads_as_empty_manifest()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "manifest.json"), "{ not valid json !!");
        var codec = new MemoryManifestCodec();

        var manifest = await codec.ReadAsync(_root);

        Assert.Equal(0, manifest.EntryCount);
    }

    [Fact]
    public async Task Partial_json_with_missing_fields_yields_defaults()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "manifest.json"), "{\"version\":\"9\"}");
        var codec = new MemoryManifestCodec();

        var manifest = await codec.ReadAsync(_root);

        Assert.Equal("9", manifest.Version);
        Assert.Equal(0, manifest.EntryCount);
    }

    [Fact]
    public async Task Constructor_rejects_null_root_on_read_and_write()
    {
        var codec = new MemoryManifestCodec();
        await Assert.ThrowsAsync<ArgumentException>(() => codec.ReadAsync(" "));
        await Assert.ThrowsAsync<ArgumentNullException>(() => codec.WriteAsync(_root, null!));
    }

    [Fact]
    public async Task Write_creates_missing_root_directory()
    {
        var freshRoot = Path.Combine(_root, "fresh", "nested");
        var codec = new MemoryManifestCodec();

        await codec.WriteAsync(freshRoot, new MemoryManifest("1", 1, 1));
        var read = await codec.ReadAsync(freshRoot);

        Assert.Equal(new MemoryManifest("1", 1, 1), read);
    }
}