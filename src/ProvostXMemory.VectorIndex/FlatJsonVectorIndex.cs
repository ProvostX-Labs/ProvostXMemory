using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProvostXMemory.VectorIndex;

/// <summary>
/// Default zero-dependency backend over the JSON namespace-file format:
/// brute-force cosine (dot product over L2-normalized vectors), transactional flush.
/// </summary>
public sealed class FlatJsonVectorIndex : IVectorIndex
{
    private readonly VectorNamespaceFile _file;
    private readonly IVectorIndexGuard _guard;
    private readonly Dictionary<string, float[]> _vectors;
    private readonly ScalarVectorOps _math;
    private bool _dirty;

    public FlatJsonVectorIndex(VectorNamespaceFile file, IVectorIndexGuard guard)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(guard);
        _file = file;
        _guard = guard;
        _math = new ScalarVectorOps();
        var content = file.Load();
        _vectors = new Dictionary<string, float[]>(content.Entries.Count, StringComparer.Ordinal);
        foreach (var entry in content.Entries)
        {
            _vectors.Add(entry.Id, NormalizeStored(entry.Vector));
        }

        _dirty = false;
    }

    public int Dim => _guard.Dim;

    /// <summary>True when mutations are not yet persisted via FlushAsync.</summary>
    public bool HasUnflushedChanges => Volatile.Read(ref _dirty);

    public long Count => _vectors.Count;

    public Task BuildAsync(IReadOnlyList<VectorItem> items, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                var content = _file.Load();
                var builder = new Dictionary<string, float[]>(content.Entries.Count, StringComparer.Ordinal);
                foreach (var entry in content.Entries)
                {
                    builder.Add(entry.Id, NormalizeStored(entry.Vector));
                }

                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    builder[item.Id] = NormalizeChecked(item);
                }

                _vectors.Clear();
                foreach (var pair in builder)
                {
                    _vectors.Add(pair.Key, pair.Value);
                }

                _dirty = true;
            },
            cancellationToken);
    }

    public Task AddAsync(VectorItem item, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                _vectors[item.Id] = NormalizeChecked(item);
                _dirty = true;
            },
            cancellationToken);
    }

    public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                if (!_vectors.Remove(id))
                {
                    throw new VectorIndexIdNotFoundException(id);
                }

                _dirty = true;
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<ScoredVectorId>> SearchAsync(
        IReadOnlyList<float> vector,
        int k,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                if (k < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(k));
                }

                var normalized = NormalizeIncoming(vector);
                var scored = new List<ScoredVectorId>(_vectors.Count);
                foreach (var pair in _vectors)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    scored.Add(new ScoredVectorId(pair.Key, _math.Cosine(normalized, pair.Value)));
                }

                scored.Sort(static (a, b) =>
                {
                    var byScore = b.Score.CompareTo(a.Score);
                    return byScore != 0 ? byScore : string.CompareOrdinal(a.Id, b.Id);
                });

                if (k < scored.Count)
                {
                    scored.RemoveRange(k, scored.Count - k);
                }

                return (IReadOnlyList<ScoredVectorId>)scored;
            },
            cancellationToken);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entries = new List<VectorNamespaceFile.Entry>(_vectors.Count);
                foreach (var pair in _vectors)
                {
                    entries.Add(new VectorNamespaceFile.Entry(pair.Key, pair.Value));
                }

                _file.Save(new VectorNamespaceFile.Content(
                    new VectorIndexGuard(_guard.ModelHash, _guard.Dim),
                    entries));
                _dirty = false;
            },
            cancellationToken);
    }

    private float[] NormalizeChecked(VectorItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Vector.Count != _guard.Dim)
        {
            throw new VectorIndexGuardMismatchException(
                $"Vector dimension mismatch: index dim {_guard.Dim} != item dim {item.Vector.Count} (id '{item.Id}').");
        }

        var buffer = new float[item.Vector.Count];
        for (var i = 0; i < item.Vector.Count; i++)
        {
            buffer[i] = item.Vector[i];
        }

        return _math.Normalize(buffer);
    }

    private float[] NormalizeStored(IReadOnlyList<float> vector)
    {
        if (vector.Count != _guard.Dim)
        {
            throw new VectorIndexGuardMismatchException(
                $"Stored vector dim mismatch in '{_file.Path}': index dim {_guard.Dim} != stored dim {vector.Count}.");
        }

        var buffer = new float[vector.Count];
        for (var i = 0; i < vector.Count; i++)
        {
            buffer[i] = vector[i];
        }

        return _math.Normalize(buffer);
    }

    private float[] NormalizeIncoming(IReadOnlyList<float> vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        if (vector.Count != _guard.Dim)
        {
            throw new VectorIndexGuardMismatchException(
                $"Vector dimension mismatch: index dim {_guard.Dim} != query dim {vector.Count}.");
        }

        var buffer = new float[vector.Count];
        for (var i = 0; i < vector.Count; i++)
        {
            buffer[i] = vector[i];
        }

        return _math.Normalize(buffer);
    }
}