using System;
using System.Collections.Generic;

namespace ProvostXMemory.VectorIndex.Engine;

/// <summary>
/// Own HNSW construction (canonical INSERT algorithm, level-0 full beam): inserts each item
/// in order, greedily descends from the entry point through layers above the node's top
/// level, then beam-inserts per layer with the M / maxM neighbor pruning heuristic
/// (keep-if-closer-than-direct-neighbors). Writes backlinks for pruned victims.
/// Deterministic: seeded level assignment + insertion order + deterministic tie-breaks.
/// </summary>
public sealed class HnswBuilder
{
    private readonly HnswOptions _options;
    private readonly VectorScanner _scanner;
    private readonly HnswLevelAssigner _assigner;
    private HnswBeamSearcher _searcher = null!;
    private HnswSearchContext _context = null!;
    private HnswGraph _graph = new(minLayerCapacity: 8);
    private float[][] _rows = Array.Empty<float[]>();

    public HnswBuilder(HnswOptions options, VectorScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scanner);
        _options = options;
        _scanner = scanner;
        _assigner = new HnswLevelAssigner(options.LevelFactor, options.Seed);
    }

    /// <summary>Runs a full construction over normalized rows (row order = insertion order).</summary>
    public HnswGraph Build(float[][] normalizedRows)
    {
        ArgumentNullException.ThrowIfNull(normalizedRows);
        _context = new HnswSearchContext(ids: new string[normalizedRows.Length], normalizedRows);
        _searcher = new HnswBeamSearcher(_context);
        _graph = new HnswGraph(minLayerCapacity: 8);
        _rows = normalizedRows;

        for (var node = 0; node < normalizedRows.Length; node++)
        {
            Insert(node);
        }

        return _graph;
    }

    private void Insert(int node)
    {
        var topLevel = node == 0 ? 0 : _assigner.NextTopLevel();
        // Canonical INSERT: descent and beams run from the PREVIOUS entry point. AddNode
        // republishes the entry when this node draws a new max level — but the new node must
        // first be linked IN via beams seeded from the old entry, never from itself.
        var previousEntry = _graph.IsEmpty ? -1 : _graph.EntryNode;
        _graph.AddNode(node, topLevel);
        if (node == 0 || previousEntry < 0)
        {
            return; // first node: entry point only, no links yet
        }

        var query = (ReadOnlySpan<float>)_rows[node];
        _context.SetQuery(query);
        _context.ResetForNewQuery();

        var current = previousEntry;

        // Greedy descent through layers above the node's top level.
        for (var layer = _graph.MaxLevel; layer > topLevel; layer--)
        {
            current = GreedyDescend(query, current, layer);
        }

        // Beam insert on each layer from min(topLevel, MaxLevel) down to 0.
        var topInsert = Math.Min(topLevel, _graph.MaxLevel);
        for (var layer = topInsert; layer >= 0; layer--)
        {
            var entryPoints = new List<HnswCandidate> { new(Math.Max(current, 0), NodeScore(Math.Max(current, 0), query)) };
            var ef = layer == 0
                ? _options.EfConstruction
                : Math.Min(_options.EfConstruction, Math.Max(_options.M + 1, 16));
            var found = _searcher.SearchLayer(entryPoints[0].Node, ef, layer, _graph);

            var limit = layer == 0 ? _options.MaxLevel0Connections : _options.M;
            var selected = SelectNeighborsHeuristic(node, found, limit);

            var links = new List<int>(selected.Count);
            foreach (var candidate in selected)
            {
                links.Add(candidate.Node);
                _graph.AddLinks(candidate.Node, layer, new[] { node });
            }

            _graph.SetNeighbors(node, layer, links);
            if (selected.Count > 0)
            {
                current = selected[0].Node;
            }
        }
    }

    private float NodeScore(int node, ReadOnlySpan<float> query) => VectorMath.Dot(query, _rows[node]);

    private int GreedyDescend(ReadOnlySpan<float> query, int start, int layer)
    {
        // Upper-layer navigation: repeatedly move to the best unvisited neighbor.
        var current = start;
        var currentScore = NodeScore(current, query);
        var visited = new HashSet<int> { current };
        var improved = true;
        while (improved)
        {
            improved = false;
            foreach (var neighbor in _graph.Neighbors(current, layer))
            {
                if (!visited.Add(neighbor))
                {
                    continue;
                }

                var score = NodeScore(neighbor, query);
                if (score > currentScore)
                {
                    current = neighbor;
                    currentScore = score;
                    improved = true;
                }
            }
        }

        return current;
    }

    /// <summary>
    /// Canonical neighbor-selection heuristic with keepPrunedConnections (paper default):
    /// scan candidates best→worst; keep a candidate only when it is closer to the base point
    /// than to every already-selected neighbor (diversity rule); afterwards backfill the
    /// best pruned candidates up to the limit so near-tied cluster regions stay connected
    /// (pure diversity pruning starves dense cliques of links). Ties break by node id.
    /// </summary>
    private List<HnswCandidate> SelectNeighborsHeuristic(int baseNode, List<HnswCandidate> candidates, int limit)
    {
        var selected = new List<HnswCandidate>(limit);
        var pruned = new List<HnswCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (candidate.Node == baseNode)
            {
                continue;
            }

            var keep = true;
            foreach (var chosen in selected)
            {
                var toChosen = _scanner.Score(_rows[candidate.Node], _rows[chosen.Node]);
                if (toChosen > candidate.Score)
                {
                    keep = false;
                    break;
                }
            }

            if (keep)
            {
                selected.Add(candidate);
            }
            else
            {
                pruned.Add(candidate);
            }

            if (selected.Count == limit)
            {
                break;
            }
        }

        for (var i = 0; selected.Count < limit && i < pruned.Count; i++)
        {
            selected.Add(pruned[i]);
        }

        return selected;
    }
}