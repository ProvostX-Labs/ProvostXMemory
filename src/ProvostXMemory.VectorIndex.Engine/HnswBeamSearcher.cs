using System;
using System.Collections.Generic;

namespace ProvostXMemory.VectorIndex.Engine;

/// <summary>
/// Exact analog of the canonical HNSW SEARCH-LAYER routine, in score form (higher = closer):
/// candidate heap = max-heap by score (top = nearest candidate, expanded first), result heap
/// = min-heap by score bounded to ef (top = worst kept result = the stop gate). A candidate
/// whose score falls below the gate stops the beam. The visited set resets per layer
/// traversal (canonical: fresh visited per searchLayer call); the query score cache lives in
/// <see cref="HnswSearchContext"/> and persists across layers of the same query. Returns
/// results sorted best-first (score desc, node id asc).
/// </summary>
public sealed class HnswBeamSearcher
{
    private readonly HnswSearchContext _context;

    public HnswBeamSearcher(HnswSearchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Beam-searches one layer starting from <paramref name="entryNode"/> (current best node
    /// from the previous layer), keeping the ef best results in descending score order.
    /// </summary>
    public List<HnswCandidate> SearchLayer(int entryNode, int ef, int layer, HnswGraph graph)
    {
        if (ef <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ef));
        }

        var visited = _context.Visited;
        visited.Clear(); // canonical: fresh visited per layer traversal

        var candidates = new CandidateHeap(ef + 1);
        var results = new ResultHeap(ef);

        var entryScore = _context.Score(entryNode);
        visited.Add(entryNode);
        candidates.Push(new HnswCandidate(entryNode, entryScore));
        results.Push(new HnswCandidate(entryNode, entryScore));

        while (candidates.Count > 0)
        {
            var current = candidates.Pop(); // nearest remaining candidate
            var gate = results.Peek(); // worst kept result
            if (current.Score < gate && results.Count >= ef)
            {
                break;
            }

            foreach (var neighbor in graph.Neighbors(current.Node, layer))
            {
                if (!visited.Add(neighbor))
                {
                    continue;
                }

                var score = _context.Score(neighbor);
                if (results.Count < ef || score > gate)
                {
                    candidates.Push(new HnswCandidate(neighbor, score));

                    results.Push(new HnswCandidate(neighbor, score));
                    if (results.Count > ef)
                    {
                        results.Pop(); // evict the worst kept result
                    }

                    gate = results.Peek();
                }
            }
        }

        // Zero-safe drain: nodes tied at score 0 (orthogonal / zero-vector queries) may never
        // enter a full result heap via the strict-improvement gate; the k-result contract
        // still requires them, so admit distinct visited nodes best-first as a fallback.
        if (results.Count < ef)
        {
            var overflow = new List<HnswCandidate>(visited.Count);
            foreach (var node in visited)
            {
                if (!results.Contains(node))
                {
                    overflow.Add(new HnswCandidate(node, _context.Score(node)));
                }
            }

            overflow.Sort(CompareBestFirst);
            foreach (var candidate in overflow)
            {
                results.Push(candidate);
                if (results.Count >= ef)
                {
                    break;
                }
            }
        }

        return results.DrainBestFirst();
    }

    /// <summary>Deterministic best-first ordering: score desc, then node id asc.</summary>
    // Stateless utility (pure): comparator over two candidates only.
    public static int CompareBestFirst(HnswCandidate a, HnswCandidate b)
    {
        var byScore = b.Score.CompareTo(a.Score);
        return byScore != 0 ? byScore : a.Node.CompareTo(b.Node);
    }

    /// <summary>Max-heap by score over <see cref="HnswCandidate"/> (top = nearest).</summary>
    private sealed class CandidateHeap
    {
        private readonly List<HnswCandidate> _items;

        public CandidateHeap(int capacity) => _items = new List<HnswCandidate>(capacity);

        public int Count => _items.Count;

        public void Push(HnswCandidate candidate)
        {
            _items.Add(candidate);
            SiftUp(_items.Count - 1);
        }

        public HnswCandidate Pop()
        {
            var top = _items[0];
            var last = _items[^1];
            _items.RemoveAt(_items.Count - 1);
            if (_items.Count > 0)
            {
                _items[0] = last;
                SiftDown(0);
            }

            return top;
        }

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                var parent = (index - 1) / 2;
                if (Above(_items[index], _items[parent]))
                {
                    (_items[index], _items[parent]) = (_items[parent], _items[index]);
                    index = parent;
                    continue;
                }

                break;
            }
        }

        private void SiftDown(int index)
        {
            while (true)
            {
                var left = (2 * index) + 1;
                var right = left + 1;
                var top = index;
                if (left < _items.Count && Above(_items[left], _items[top]))
                {
                    top = left;
                }

                if (right < _items.Count && Above(_items[right], _items[top]))
                {
                    top = right;
                }

                if (top == index)
                {
                    return;
                }

                (_items[index], _items[top]) = (_items[top], _items[index]);
                index = top;
            }
        }

        // Stateless utility (pure): heap ordering comparator, no state.
        private static bool Above(HnswCandidate a, HnswCandidate b)
            => a.Score != b.Score ? a.Score > b.Score : a.Node < b.Node;
    }

    /// <summary>Bounded min-heap by score (top = worst kept result = the beam gate).</summary>
    private sealed class ResultHeap
    {
        private readonly List<HnswCandidate> _items;
        private readonly int _capacity;

        public ResultHeap(int capacity)
        {
            _capacity = capacity;
            _items = new List<HnswCandidate>(capacity + 1);
        }

        public int Count => _items.Count;

        /// <summary>Worst kept result's score; negative infinity while not yet full.</summary>
        public float Peek() => _items.Count == 0 ? float.NegativeInfinity : _items[0].Score;

        public bool Contains(int node)
        {
            foreach (var candidate in _items)
            {
                if (candidate.Node == node)
                {
                    return true;
                }
            }

            return false;
        }

        public void Push(HnswCandidate candidate)
        {
            _items.Add(candidate);
            SiftUp(_items.Count - 1);
        }

        public HnswCandidate Pop()
        {
            var top = _items[0];
            var last = _items[^1];
            _items.RemoveAt(_items.Count - 1);
            if (_items.Count > 0)
            {
                _items[0] = last;
                SiftDown(0);
            }

            return top;
        }

        public List<HnswCandidate> DrainBestFirst()
        {
            var drained = new List<HnswCandidate>(_items);
            drained.Sort(CompareBestFirst);
            return drained;
        }

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                var parent = (index - 1) / 2;
                if (Above(_items[index], _items[parent]))
                {
                    (_items[index], _items[parent]) = (_items[parent], _items[index]);
                    index = parent;
                    continue;
                }

                break;
            }
        }

        private void SiftDown(int index)
        {
            while (true)
            {
                var left = (2 * index) + 1;
                var right = left + 1;
                var top = index;
                if (left < _items.Count && Above(_items[left], _items[top]))
                {
                    top = left;
                }

                if (right < _items.Count && Above(_items[right], _items[top]))
                {
                    top = right;
                }

                if (top == index)
                {
                    return;
                }

                (_items[index], _items[top]) = (_items[top], _items[index]);
                index = top;
            }
        }

        // Stateless utility (pure): heap ordering comparator, no state.
        private static bool Above(HnswCandidate a, HnswCandidate b)
            => a.Score != b.Score ? a.Score < b.Score : a.Node < b.Node;
    }
}