using System;
using System.Collections.Generic;

namespace ProvostXMemory.VectorIndex.Engine;

/// <summary>
/// Query-time traversal over a built HNSW graph: greedy descent from the entry point
/// through upper layers to layer 1, then a single ef-bounded beam search on layer 0,
/// sorted best-first. Read-only over the published immutable graph.
/// </summary>
public sealed class HnswSearcher
{
    private readonly HnswOptions _options;
    private readonly HnswSearchContext _context;
    private readonly HnswBeamSearcher _beamSearcher;

    public HnswSearcher(HnswOptions options, HnswSearchContext context)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);
        _options = options;
        _context = context;
        _beamSearcher = new HnswBeamSearcher(context);
    }

    /// <summary>
    /// Searches the graph for the k nearest nodes to the normalized query. Returns node ids
    /// ordered by score descending (ties: node id ascending). Empty when the graph is empty.
    /// </summary>
    public List<(int Node, float Score)> Search(
        HnswGraph graph,
        float[][] normalizedRows,
        ReadOnlySpan<float> normalizedQuery,
        int k)
    {
        if (k < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k));
        }

        var hits = new List<(int Node, float Score)>();
        if (graph.IsEmpty || k == 0)
        {
            return hits;
        }

        _context.SetQuery(normalizedQuery);
        _context.ResetForNewQuery();

        var current = graph.EntryNode;

        // Greedy descent through upper layers (classic ef=1 upper-layer navigation).
        for (var layer = graph.MaxLevel; layer > 0; layer--)
        {
            var currentScore = _context.Score(current);
            var improved = true;
            while (improved)
            {
                improved = false;
                foreach (var neighbor in graph.Neighbors(current, layer))
                {
                    var score = _context.Score(neighbor);
                    if (score > currentScore)
                    {
                        current = neighbor;
                        currentScore = score;
                        improved = true;
                    }
                }
            }
        }

        // Beam search on layer 0 with ef ≥ k.
        var ef = Math.Max(Math.Max(_options.EfSearch, k), 1);
        var found = _beamSearcher.SearchLayer(current, ef, layer: 0, graph);

        // Honor k exactly: the beam keeps ef ≥ k results — truncate to the k best.
        foreach (var candidate in found.Take(k))
        {
            hits.Add((candidate.Node, candidate.Score));
        }

        return hits;
    }
}