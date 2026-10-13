using System;
using System.Collections.Generic;

namespace ProvostXMemory.VectorIndex;

/// <summary>
/// Mutable in-memory store shared by engine indexes: ids in row order parallel to vector
/// rows plus id → row map. Owns synchronization; build/add/remove/dim-guard semantics.
/// </summary>
public sealed class VectorItemStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, int> _rowOfId;
    private readonly List<string> _idsInRowOrder;
    private readonly List<float[]> _vectors;

    public VectorItemStore(int dim)
    {
        if (dim <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dim));
        }

        _rowOfId = new Dictionary<string, int>(StringComparer.Ordinal);
        _idsInRowOrder = new List<string>();
        _vectors = new List<float[]>();
        Dim = dim;
    }

    public int Dim { get; }

    public long Count
    {
        get
        {
            lock (_lock)
            {
                return _vectors.Count;
            }
        }
    }

    /// <summary>Insert-or-replace by id (copy-by-value). Throws on dim mismatch.</summary>
    public void Set(string id, ReadOnlySpan<float> vector)
    {
        if (vector.Length != Dim)
        {
            throw new VectorIndexGuardMismatchException(
                $"Vector dimension mismatch: store dim {Dim} != item dim {vector.Length} (id '{id}').");
        }

        var copy = vector.ToArray();
        lock (_lock)
        {
            if (_rowOfId.TryGetValue(id, out var existingRow))
            {
                _vectors[existingRow] = copy;
                return;
            }

            _rowOfId.Add(id, _vectors.Count);
            _idsInRowOrder.Add(id);
            _vectors.Add(copy);
        }
    }

    /// <summary>Removes by id via swap-with-last-row; false when unknown.</summary>
    public bool Remove(string id)
    {
        lock (_lock)
        {
            if (!_rowOfId.TryGetValue(id, out var row))
            {
                return false;
            }

            var lastRow = _vectors.Count - 1;
            if (row != lastRow)
            {
                var movedId = _idsInRowOrder[lastRow];
                _vectors[row] = _vectors[lastRow];
                _idsInRowOrder[row] = movedId;
                _rowOfId[movedId] = row;
            }

            _vectors.RemoveAt(lastRow);
            _idsInRowOrder.RemoveAt(lastRow);
            _rowOfId.Remove(id);
            return true;
        }
    }

    /// <summary>Contains check.</summary>
    public bool Contains(string id)
    {
        lock (_lock)
        {
            return _rowOfId.ContainsKey(id);
        }
    }

    /// <summary>Row of a live id; -1 when unknown. Swap-removals keep rows stable until mutation.</summary>
    public int RowOf(string id)
    {
        lock (_lock)
        {
            return _rowOfId.TryGetValue(id, out var row) ? row : -1;
        }
    }

    /// <summary>Id living at a row (row-order snapshot semantics); caller must hold a consistent snapshot.</summary>
    public string IdAt(int row)
    {
        lock (_lock)
        {
            return _idsInRowOrder[row];
        }
    }

    /// <summary>Point lookup of a live row; null when unknown.</summary>
    public float[]? TryGetVector(string id)
    {
        lock (_lock)
        {
            if (_rowOfId.TryGetValue(id, out var row))
            {
                return _vectors[row];
            }

            return null;
        }
    }

    /// <summary>Consistent snapshot: ids[i] ↔ vectors[i] in row order, copied under lock.</summary>
    public (string[] Ids, float[][] Vectors) SnapshotRows()
    {
        lock (_lock)
        {
            return (_idsInRowOrder.ToArray(), _vectors.ToArray());
        }
    }

    /// <summary>Clears all rows.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _rowOfId.Clear();
            _idsInRowOrder.Clear();
            _vectors.Clear();
        }
    }
}