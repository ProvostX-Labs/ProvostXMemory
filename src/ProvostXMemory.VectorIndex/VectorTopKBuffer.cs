using System;
using System.Collections.Generic;

namespace ProvostXMemory.VectorIndex;

/// <summary>Bounded top-k min-heap buffer over (id, score); scores rise to the top.</summary>
public sealed class VectorTopKBuffer
{
    private readonly List<(string Id, float Score)> _heap;

    public VectorTopKBuffer(int k)
    {
        if (k < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k));
        }

        Capacity = k;
        _heap = new List<(string Id, float Score)>(k + 1);
    }

    public int Capacity { get; }

    public int Count => _heap.Count;

    /// <summary>Lowest score currently retained (the eviction threshold); 0-arg safe when empty.</summary>
    public float WorstScore => _heap.Count == 0 ? float.NegativeInfinity : _heap[0].Score;

    /// <summary>Offers a candidate; evicts the weakest when over capacity.</summary>
    public void Offer(string id, float score)
    {
        if (Capacity == 0)
        {
            return;
        }

        if (_heap.Count < Capacity)
        {
            _heap.Add((id, score));
            SiftUp(_heap.Count - 1);
            return;
        }

        if (score > _heap[0].Score)
        {
            _heap[0] = (id, score);
            SiftDown(0);
        }
    }

    /// <summary>Drains entries in descending score order (ties broken by id, ascending).</summary>
    public List<(string Id, float Score)> Drain()
    {
        var entries = new List<(string Id, float Score)>(_heap);
        entries.Sort(static (a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : string.CompareOrdinal(a.Id, b.Id);
        });

        _heap.Clear();
        return entries;
    }

    private void SiftUp(int index)
    {
        while (index > 0)
        {
            var parent = (index - 1) / 2;
            if (_heap[index].Score < _heap[parent].Score)
            {
                (_heap[index], _heap[parent]) = (_heap[parent], _heap[index]);
                index = parent;
            }
            else
            {
                break;
            }
        }
    }

    private void SiftDown(int index)
    {
        while (true)
        {
            var left = (2 * index) + 1;
            var right = left + 1;
            var smallest = index;
            if (left < _heap.Count && _heap[left].Score < _heap[smallest].Score)
            {
                smallest = left;
            }

            if (right < _heap.Count && _heap[right].Score < _heap[smallest].Score)
            {
                smallest = right;
            }

            if (smallest == index)
            {
                return;
            }

            (_heap[index], _heap[smallest]) = (_heap[smallest], _heap[index]);
            index = smallest;
        }
    }
}