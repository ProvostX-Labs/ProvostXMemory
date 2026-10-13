using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProvostXMemory.VectorIndex.Engine;

/// <summary>
/// Own-engine HNSW backend with IMMUTABLE-INDEX + BATCH-REBUILD semantics (patent-avoidance
/// design, ARCHITECTURE.md addendum 2026-10-13): the layered graph is fully rebuilt from the
/// item store on every Build, Add, and Remove — there is deliberately NO incremental update
/// of a live graph, the method covered by US 15/929,802 "Dynamic Updates for HNSW". Core
/// construction + layered greedy/beam search (unpatented) is implemented from scratch.
/// Mutations mark the published graph dirty; the next search rebuilds it once (same
/// batch-rebuild behavior as <see cref="IvfFlatIndex"/>). Cosine metric: vectors are
/// L2-normalized internally. Deterministic given the same items in the same order + seed.
/// </summary>
public sealed class HnswIndex : IVectorIndex
{
    private readonly object _lock = new();
    private readonly VectorItemStore _store;
    private readonly IVectorIndexGuard _guard;
    private readonly HnswOptions _options;
    private HnswBuilder _builder = null!;
    private HnswSearcher? _searcher;
    private HnswSearchContext? _searchContext;
    private volatile HnswPublishedGraph? _published;

    public HnswIndex(IVectorIndexGuard guard)
        : this(guard, new HnswOptions())
    {
    }

    public HnswIndex(IVectorIndexGuard guard, HnswOptions options)
    {
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(options);
        _guard = guard;
        _options = options;
        _store = new VectorItemStore(guard.Dim);
    }

    public int Dim => _guard.Dim;

    public long Count => _store.Count;

    /// <summary>Graph stats of the last built snapshot (diagnostics).</summary>
    public (int Nodes, int MaxLevel, int EntryNode) GraphStats
    {
        get
        {
            lock (_lock)
            {
                return _published is null ? (0, 0, -1) : (_published.Graph.MaxLevel + 1, _published.Graph.MaxLevel, _published.Graph.EntryNode);
            }
        }
    }

    public Task BuildAsync(IReadOnlyList<VectorItem> items, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                lock (_lock)
                {
                    _store.Clear();
                    foreach (var item in items)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _store.Set(item.Id, CopyChecked(item));
                    }

                    RebuildGraphLocked();
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Adding is implemented as store write + full graph rebuild (batch-rebuild, not an
    /// incremental dynamic update — see class doc).
    /// </summary>
    public Task AddAsync(VectorItem item, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                lock (_lock)
                {
                    _store.Set(item.Id, CopyChecked(item));
                    MarkDirtyLocked();
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Removing is implemented as store delete + full graph rebuild (batch-rebuild, not an
    /// incremental dynamic update — see class doc).
    /// </summary>
    public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                lock (_lock)
                {
                    if (!_store.Remove(id))
                    {
                        throw new VectorIndexIdNotFoundException(id);
                    }

                    MarkDirtyLocked();
                }
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

                var query = CopyCheckedQuery(vector);
                lock (_lock)
                {
                    if (_published is null)
                    {
                        RebuildGraphLocked(); // lazy first build / rebuild-after-mutation
                    }

                    var published = _published;
                    if (published is null || k == 0)
                    {
                        return (IReadOnlyList<ScoredVectorId>)Array.Empty<ScoredVectorId>();
                    }

                    EnsureSearcherLocked(published);
                    var normalizedQuery = VectorMath.Normalize(query);
                    var found = _searcher!.Search(published.Graph, published.NormalizedRows, normalizedQuery, k);

                    var hits = new List<ScoredVectorId>(found.Count);
                    foreach (var (node, score) in found)
                    {
                        hits.Add(new ScoredVectorId(published.Ids[node], score));
                    }

                    return (IReadOnlyList<ScoredVectorId>)hits;
                }
            },
            cancellationToken);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        // Fully in-memory backend; the graph is rebuilt from the store on demand, nothing persisted.
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
            },
            cancellationToken);
    }

    /// <summary>Full construction from the current store — the ONLY way the graph changes.</summary>
    private void RebuildGraphLocked()
    {
        var (ids, rows) = _store.SnapshotRows();
        if (rows.Length == 0)
        {
            _published = null;
            return;
        }

        var normalized = new float[rows.Length][];
        for (var r = 0; r < rows.Length; r++)
        {
            normalized[r] = VectorMath.Normalize(rows[r]);
        }

        // Fresh builder per build: seeded level assigner must restart per rebuild for
        // determinism (same items + same order + same seed → same graph).
        _builder = new HnswBuilder(_options, new VectorScanner(VectorDistanceMetric.Cosine));
        var graph = _builder.Build(normalized);
        _published = new HnswPublishedGraph(ids, graph, normalized);
        _searcher = null;
    }

    private void EnsureSearcherLocked(HnswPublishedGraph published)
    {
        if (_searcher is null || _searchContext is null || _searchContext.NormalizedRows.Length != published.NormalizedRows.Length)
        {
            _searchContext = new HnswSearchContext(published.Ids, published.NormalizedRows);
            _searcher = new HnswSearcher(_options, _searchContext);
        }
    }

    private void MarkDirtyLocked()
    {
        // Batch-rebuild semantics: drop the published graph; the next search triggers a full
        // rebuild from the store (never an incremental patch of a live graph).
        _published = null;
        _searcher = null;
    }

    private float[] CopyChecked(VectorItem item)
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

        return buffer;
    }

    private float[] CopyCheckedQuery(IReadOnlyList<float> vector)
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

        return buffer;
    }
}