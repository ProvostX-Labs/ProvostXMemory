using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ProvostXMemory.VectorIndex;

/// <summary>
/// On-disk JSON vector index file format (one file per scope/model-hash namespace):
/// temp-file + atomic swap on write; open enforces the model-hash/dim guard.
/// </summary>
public sealed class VectorNamespaceFile
{
    private const string FileId = "provostx.vectorindex.v1";

    // Stateless utility (pure): frozen serializer options, immutable after first use.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    private readonly string _path;
    private readonly string _modelHash;
    private readonly int _dim;

    public VectorNamespaceFile(string path, string modelHash, int dim)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(modelHash);
        ArgumentOutOfRangeException.ThrowIfNegative(dim);
        _path = path;
        _modelHash = modelHash;
        _dim = dim;
    }

    public string Path => _path;

    public string ModelHash => _modelHash;

    public int Dim => _dim;

    /// <summary>Throws when the candidate model hash/dim does not guard-match this namespace.</summary>
    public void EnsureGuard(string candidateModelHash, int candidateDim)
    {
        if (!string.Equals(candidateModelHash, _modelHash, StringComparison.Ordinal))
        {
            throw new VectorIndexGuardMismatchException(
                $"Model hash mismatch: namespace '{_modelHash}' != candidate '{candidateModelHash}'.");
        }

        if (candidateDim != _dim)
        {
            throw new VectorIndexGuardMismatchException(
                $"Vector dimension mismatch: namespace dim {_dim} != candidate dim {candidateDim}.");
        }
    }

    /// <summary>Sets are keyed by id; a re-set id replaces the entry.</summary>
    public sealed class Entry
    {
        public Entry(string id, IReadOnlyList<float> vector)
        {
            ArgumentNullException.ThrowIfNull(id);
            ArgumentNullException.ThrowIfNull(vector);
            Id = id;
            Vector = vector;
        }

        public string Id { get; }

        public IReadOnlyList<float> Vector { get; }
    }

    /// <summary>Content of one namespace file: guard + entries.</summary>
    public sealed class Content
    {
        public Content(VectorIndexGuard guard, IReadOnlyList<Entry> entries)
        {
            ArgumentNullException.ThrowIfNull(guard);
            ArgumentNullException.ThrowIfNull(entries);
            Guard = guard;
            Entries = entries;
        }

        public VectorIndexGuard Guard { get; }

        public IReadOnlyList<Entry> Entries { get; }
    }

    /// <summary>Reads the namespace file; a missing file yields empty content. Guard-checked.</summary>
    public Content Load()
    {
        if (!File.Exists(_path))
        {
            return new Content(new VectorIndexGuard(_modelHash, _dim), Array.Empty<Entry>());
        }

        string json;
        try
        {
            json = File.ReadAllText(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Failed reading index file '{_path}'.", ex);
        }

        JsonModel? model;
        try
        {
            model = JsonSerializer.Deserialize<JsonModel>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid index file format in '{_path}'.", ex);
        }

        if (model is null
            || string.IsNullOrEmpty(model.ModelHash)
            || !string.Equals(model.FileId, FileId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unknown index file format in '{_path}'.");
        }

        EnsureGuard(model.ModelHash, model.Dim);

        var entries = new List<Entry>(model.Items?.Count ?? 0);
        if (model.Items is not null)
        {
            foreach (var item in model.Items)
            {
                if (item is null || string.IsNullOrEmpty(item.Id) || item.Vector is null)
                {
                    throw new InvalidOperationException($"Malformed entry in index file '{_path}'.");
                }

                entries.Add(new Entry(item.Id, item.Vector));
            }
        }

        return new Content(new VectorIndexGuard(model.ModelHash, model.Dim), entries);
    }

    /// <summary>Writes content atomically: serialize → temp file in same dir → swap.</summary>
    public void Save(Content content)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureGuard(content.Guard.ModelHash, content.Guard.Dim);

        var model = new JsonModel
        {
            FileId = FileId,
            ModelHash = content.Guard.ModelHash,
            Dim = content.Guard.Dim,
            Items = new List<JsonItem>(content.Entries.Count),
        };

        foreach (var entry in content.Entries)
        {
            model.Items.Add(new JsonItem
            {
                Id = entry.Id,
                Vector = entry.Vector.ToList(),
            });
        }

        var json = JsonSerializer.Serialize(model, SerializerOptions);
        AtomicWrite(json);
    }

    /// <summary>Deletes the namespace file if present.</summary>
    public void Delete()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private void AtomicWrite(string json)
    {
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = System.IO.Path.Combine(
            directory ?? ".",
            $".{System.IO.Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>Internal JSON wire model (pure data; never exposed beyond this file's codec).</summary>
    private sealed class JsonModel
    {
        public string? FileId { get; set; }

        public string? ModelHash { get; set; }

        public int Dim { get; set; }

        public List<JsonItem>? Items { get; set; }
    }

    private sealed class JsonItem
    {
        public string? Id { get; set; }

        public List<float>? Vector { get; set; }
    }
}