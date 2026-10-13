using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Storage;

/// <summary>Manifest read/write for a memory root (JSON, atomic swap); missing file reads as empty.</summary>
public sealed class MemoryManifestCodec : IMemoryManifestCodec
{
    // Pure factory of JSON serializer options; immutable after creation, no shared mutable state.
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private const string ManifestFileName = "manifest.json";
    private const string FallbackVersion = "0";
    private const string FallbackUpdatedAtUnixMs = "0";

    private readonly AtomicFileWriter _writer;

    public MemoryManifestCodec(AtomicFileWriter? writer = null)
    {
        _writer = writer ?? new AtomicFileWriter(); // pure factory default; no shared mutable state
    }

    /// <summary>Reads <c>&lt;root&gt;/manifest.json</c>; a missing or empty file yields an empty manifest.</summary>
    public Task<MemoryManifest> ReadAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Root path is required.", nameof(rootPath));
        }

        var path = Path.Combine(rootPath, ManifestFileName);
        if (!File.Exists(path))
        {
            return Task.FromResult(EmptyManifest());
        }

        using var stream = File.OpenRead(path);
        if (stream.Length == 0)
        {
            return Task.FromResult(EmptyManifest());
        }

        cancellationToken.ThrowIfCancellationRequested();
        MemoryManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<MemoryManifest>(stream, JsonOptions);
        }
        catch (JsonException)
        {
            return Task.FromResult(EmptyManifest());
        }

        return Task.FromResult(manifest ?? EmptyManifest());
    }

    /// <summary>Writes the manifest to <c>&lt;root&gt;/manifest.json</c> transactionally.</summary>
    public Task WriteAsync(string rootPath, MemoryManifest manifest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(manifest);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Root path is required.", nameof(rootPath));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var path = Path.Combine(rootPath, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        _writer.Write(path, json);
        return Task.CompletedTask;
    }

    // Pure function: canonical empty manifest for missing/corrupt files.
    private static MemoryManifest EmptyManifest() => new(Version: FallbackVersion, EntryCount: 0, UpdatedAtUnixMs: 0);

    // Pure factory: immutable serializer options (camelCase, case-insensitive reads, pretty output).
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };
        return options;
    }
}