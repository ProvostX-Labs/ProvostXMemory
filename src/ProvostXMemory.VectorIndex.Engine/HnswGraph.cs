using System;
using System.Collections.Generic;

namespace ProvostXMemory.VectorIndex.Engine;

/// <summary>
/// Immutable-after-build layered HNSW graph topology: per-node adjacency sets per layer,
/// plus the entry node and current max level. Pure data: ids/vectors live in
/// <see cref="VectorItemStore"/>; the graph only stores link structure (int ids). Mutations
/// happen only during construction — the finished graph is published in one reference swap
/// and never modified afterwards (immutable-index semantics, ARCHITECTURE.md addendum).
/// </summary>
public sealed class HnswGraph
{
    private List<int[]>[] _layers;
    private int _entryNode = -1;
    private int _maxLevel = -1;

    public HnswGraph(int minLayerCapacity)
    {
        if (minLayerCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minLayerCapacity));
        }

        var capacity = Math.Max(minLayerCapacity, 1);
        _layers = new List<int[]>[capacity];
        for (var layer = 0; layer < capacity; layer++)
        {
            _layers[layer] = new List<int[]>();
        }
    }

    /// <summary>Highest occupied layer (-1 for an empty graph).</summary>
    public int MaxLevel => _maxLevel;

    /// <summary>Entry node id for greedy descent (-1 when empty).</summary>
    public int EntryNode => _entryNode;

    public bool IsEmpty => _entryNode < 0;

    /// <summary>Registers a node with the given top level and resets its adjacency rows to empty.</summary>
    public void AddNode(int node, int topLevel)
    {
        if (topLevel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topLevel));
        }

        EnsureLayerCapacity(topLevel);
        for (var layer = 0; layer <= topLevel; layer++)
        {
            var rows = _layers[layer];
            while (rows.Count <= node)
            {
                rows.Add(Array.Empty<int>());
            }

            rows[node] = Array.Empty<int>();
        }

        if (topLevel > _maxLevel)
        {
            _maxLevel = topLevel;
            _entryNode = node;
        }
    }

    /// <summary>Replaces the adjacency row of a node on a layer (construction-time use).</summary>
    public void SetNeighbors(int node, int layer, List<int> neighbors)
    {
        EnsureRow(layer, node);
        _layers[layer][node] = neighbors.ToArray();
    }

    /// <summary>Appends directed links to a node's adjacency row (construction backlinks).</summary>
    public void AddLinks(int from, int layer, IReadOnlyList<int> targets)
    {
        EnsureRow(layer, from);
        var current = _layers[layer][from];
        var grown = new int[current.Length + targets.Count];
        Array.Copy(current, grown, current.Length);
        for (var i = 0; i < targets.Count; i++)
        {
            grown[current.Length + i] = targets[i];
        }

        _layers[layer][from] = grown;
    }

    /// <summary>Live adjacency of a node on a layer (empty when none).</summary>
    public ReadOnlySpan<int> Neighbors(int node, int layer)
    {
        if (layer < 0 || layer >= _layers.Length || node < 0 || node >= _layers[layer].Count)
        {
            return ReadOnlySpan<int>.Empty;
        }

        return _layers[layer][node];
    }

    private void EnsureLayerCapacity(int topLevel)
    {
        if (topLevel < _layers.Length)
        {
            return;
        }

        var grown = new List<int[]>[topLevel + 1];
        for (var layer = 0; layer < _layers.Length; layer++)
        {
            grown[layer] = _layers[layer];
        }

        for (var layer = _layers.Length; layer < grown.Length; layer++)
        {
            grown[layer] = new List<int[]>();
        }

        _layers = grown;
    }

    private void EnsureRow(int layer, int node)
    {
        if (layer < 0 || layer >= _layers.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(layer));
        }

        var rows = _layers[layer];
        while (rows.Count <= node)
        {
            rows.Add(Array.Empty<int>());
        }
    }
}