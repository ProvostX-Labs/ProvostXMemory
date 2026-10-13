using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Chunking;
using ProvostXMemory.Fusion;
using ProvostXMemory.Lexical;
using ProvostXMemory.Reranking;
using ProvostXMemory.Telemetry;
using ProvostXMemory.VectorIndex;

namespace ProvostXMemory.Retrieval;

/// <summary>
/// Hybrid retrieval within one scope: dense (vector index) and lexical (BM25-lite index)
/// searches run concurrently over the scope's registered artifacts, are fused via the
/// injected <see cref="IResultFuser"/> (RRF over "dense"/"lexical" rank lists), resolved
/// against the scope's records, and optionally reranked by an injected <see cref="IReranker"/>.
/// Scope comes from the query and all registry lookups are scope-exact.
/// </summary>
public sealed class HybridRetriever : IHybridRetriever
{
    private const string RecordAnchorPrefix = "record:";
    private const string ChunkIdPrefix = "chunk:";

    private const string DenseSource = "dense";
    private const string LexicalSource = "lexical";

    private readonly IScopeIndexRegistry _registry;
    private readonly IVectorEmbedder _embedder;
    private readonly IResultFuser _fuser;
    private readonly FusionOptions _fusionOptions;
    private readonly IReranker? _reranker;
    private readonly IMemoryTelemetry _telemetry;

    public HybridRetriever(
        IScopeIndexRegistry registry,
        IVectorEmbedder embedder,
        IResultFuser fuser,
        IMemoryTelemetry? telemetry = null,
        IReranker? reranker = null,
        FusionOptions? fusionOptions = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
        _fuser = fuser ?? throw new ArgumentNullException(nameof(fuser));
        _telemetry = telemetry ?? NoopMemoryTelemetry.Instance;
        _reranker = reranker;
        _fusionOptions = fusionOptions ?? new FusionOptions();
    }

    /// <inheritdoc />
    public async Task<RetrievalResult> RetrieveAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Scope);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.TopK);

        var scope = query.Scope;
        var denseIndex = TryGetVectorIndex(scope);
        var lexicalIndex = TryGetLexicalIndex(scope);
        var searchDepth = Math.Max(query.TopK, 1);

        // Whitespace/empty query text: nothing to embed and no lexical tokens — an
        // unconstrained dense search would return arbitrary nearest neighbors.
        if (string.IsNullOrWhiteSpace(query.Text))
        {
            return new RetrievalResult(
                Array.Empty<ScoredMemoryRecord>(),
                new[] { new RetrievalStageDiagnostics("empty-query", System.Diagnostics.Stopwatch.StartNew().Elapsed, new Dictionary<string, double>(StringComparer.Ordinal) { ["reason"] = 1 }) });
        }

        // Dense + lexical run concurrently.
        var denseTask = denseIndex is null
            ? Task.FromResult<IReadOnlyList<ScoredId>>(Array.Empty<ScoredId>())
            : DenseSearchAsync(denseIndex, query, searchDepth, cancellationToken);
        var lexicalTask = lexicalIndex is null
            ? Task.FromResult<IReadOnlyList<ScoredId>>(Array.Empty<ScoredId>())
            : LexicalSearchAsync(lexicalIndex, query, searchDepth, cancellationToken);

        var denseWatch = System.Diagnostics.Stopwatch.StartNew();
        var denseHits = await denseTask.ConfigureAwait(false);
        denseWatch.Stop();

        var lexicalWatch = System.Diagnostics.Stopwatch.StartNew();
        var lexicalHits = await lexicalTask.ConfigureAwait(false);
        lexicalWatch.Stop();

        var fusionWatch = System.Diagnostics.Stopwatch.StartNew();
        var fused = _fuser.Fuse(
            new[]
            {
                new RankedList(DenseSource, denseHits),
                new RankedList(LexicalSource, lexicalHits),
            },
            _fusionOptions);
        fusionWatch.Stop();

        var resolveWatch = System.Diagnostics.Stopwatch.StartNew();
        var resolved = await ResolveAsync(scope, fused, query, cancellationToken).ConfigureAwait(false);
        resolveWatch.Stop();

        // RetrievalFilters enforcement (post-fusion, before rerank/return): category exact,
        // tags any-match, date window on the record date. Null filters are a no-op.
        var filterWatch = System.Diagnostics.Stopwatch.StartNew();
        var beforeFilterCount = resolved.Count;
        var records = RetrievalFilterApplier.Apply(resolved, query.Filters);
        filterWatch.Stop();

        var rerankWatch = System.Diagnostics.Stopwatch.StartNew();
        if (_reranker is not null && records.Count > 0)
        {
            var reranked = await _reranker.RerankAsync(query, records, cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(reranked);
            records = reranked;
        }

        rerankWatch.Stop();
        await ReportTelemetryAsync(denseWatch, lexicalWatch, fusionWatch, resolveWatch, rerankWatch, fused.Count).ConfigureAwait(false);

        var diagnostics = new List<RetrievalStageDiagnostics>
        {
            Stage("dense", denseWatch, denseHits),
            Stage("lexical", lexicalWatch, lexicalHits),
            Stage("fusion", fusionWatch, fused),
            new(
                "resolve",
                resolveWatch.Elapsed,
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["records"] = records.Count,
                }),
            new(
                "filters",
                filterWatch.Elapsed,
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["before"] = beforeFilterCount,
                    ["records"] = records.Count,
                }),
            new(
                "rerank",
                rerankWatch.Elapsed,
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["records"] = records.Count,
                }),
        };

        return new RetrievalResult(records.Take(query.TopK).ToList(), diagnostics);
    }

    private IVectorIndex? TryGetVectorIndex(MemoryScope scope)
    {
        try
        {
            return _registry.GetVectorIndex(scope);
        }
        catch (KeyNotFoundException)
        {
            return null; // no dense index for this scope — the lexical stage still runs
        }
    }

    private ILexicalIndex? TryGetLexicalIndex(MemoryScope scope)
    {
        try
        {
            return _registry.GetLexicalIndex(scope);
        }
        catch (KeyNotFoundException)
        {
            return null; // no lexical index for this scope — the dense stage still runs
        }
    }

    private async Task<IReadOnlyList<ScoredId>> DenseSearchAsync(
        IVectorIndex index,
        RetrievalQuery query,
        int searchDepth,
        CancellationToken cancellationToken)
    {
        var queryVector = await _embedder.EmbedAsync(query.Text, cancellationToken).ConfigureAwait(false);
        if (queryVector.Length != index.Dim)
        {
            throw new InvalidOperationException(
                $"Embedder dim {queryVector.Length} does not match index dim {index.Dim} for scope '{query.Scope.Kind}/{query.Scope.Id}'.");
        }

        var hits = await index.SearchAsync(queryVector, searchDepth, cancellationToken).ConfigureAwait(false);
        return hits
            .Select(hit => new ScoredId(query.Scope.Kind.ToString(), query.Scope.Id, hit.Id, hit.Score))
            .ToList();
    }

    private static async Task<IReadOnlyList<ScoredId>> LexicalSearchAsync(
        ILexicalIndex index,
        RetrievalQuery query,
        int searchDepth,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        var tokens = LexicalTokenizer.Tokenize(query.Text);
        if (tokens.Count == 0)
        {
            return Array.Empty<ScoredId>();
        }

        return index
            .Search(tokens, searchDepth)
            .Select(hit => new ScoredId(query.Scope.Kind.ToString(), query.Scope.Id, hit.Id, hit.Score))
            .ToList();
    }

    private async Task<IReadOnlyList<ScoredMemoryRecord>> ResolveAsync(
        MemoryScope scope,
        IReadOnlyList<ScoredId> fused,
        RetrievalQuery query,
        CancellationToken cancellationToken)
    {
        if (fused.Count == 0)
        {
            return Array.Empty<ScoredMemoryRecord>();
        }

        var provider = _registry.GetRecordProvider(scope);
        var snapshot = await provider(scope, cancellationToken).ConfigureAwait(false);
        var byId = new Dictionary<string, MemoryRecord>(snapshot.Count, StringComparer.Ordinal);
        foreach (var record in snapshot)
        {
            byId[record.Id] = record;
        }

        var resolved = new List<ScoredMemoryRecord>(Math.Min(fused.Count, query.TopK));
        var codec = new ChunkIdCodec();
        foreach (var hit in fused)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recordId = ResolveParentRecordId(hit.Id, codec, query.Scope);
            if (recordId is not null && byId.TryGetValue(recordId, out var record))
            {
                resolved.Add(new ScoredMemoryRecord(record, hit.Score, new RetrievalProvenance("hybrid")));
            }
        }

        return resolved;
    }

    /// <summary>
    /// Maps an index-item id to its parent record id: "record:&lt;id&gt;" strips the
    /// anchor prefix, "chunk:&lt;codec-id&gt;" parses the canonical chunk id to its parent
    /// record (scope must match — cross-scope ids never resolve), and bare ids pass
    /// through unchanged (lexical stages may index raw record ids). Returns null for
    /// foreign ids.
    /// </summary>
    private static string? ResolveParentRecordId(string indexItemId, ChunkIdCodec codec, MemoryScope scope)
    {
        if (indexItemId.StartsWith(RecordAnchorPrefix, StringComparison.Ordinal))
        {
            return indexItemId[RecordAnchorPrefix.Length..];
        }

        if (indexItemId.StartsWith(ChunkIdPrefix, StringComparison.Ordinal))
        {
            var parts = codec.Parse(indexItemId[ChunkIdPrefix.Length..]);
            if (parts is null || parts.ScopeKind != scope.Kind || !string.Equals(parts.ScopeId, scope.Id, StringComparison.Ordinal))
            {
                return null;
            }

            return parts.RecordId;
        }

        return indexItemId;
    }

    private static RetrievalStageDiagnostics Stage(string stage, System.Diagnostics.Stopwatch watch, IReadOnlyList<ScoredId> hits)
    {
        var scores = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["hits"] = hits.Count,
        };
        if (hits.Count > 0)
        {
            scores["top-score"] = hits[0].Score;
        }

        return new RetrievalStageDiagnostics(stage, watch.Elapsed, scores);
    }

    private async Task ReportTelemetryAsync(
        System.Diagnostics.Stopwatch denseWatch,
        System.Diagnostics.Stopwatch lexicalWatch,
        System.Diagnostics.Stopwatch fusionWatch,
        System.Diagnostics.Stopwatch resolveWatch,
        System.Diagnostics.Stopwatch rerankWatch,
        int fusedCount)
    {
        await _telemetry.ReportStageAsync("retrieval.dense", denseWatch.Elapsed.TotalMilliseconds, EmptyScores()).ConfigureAwait(false);
        await _telemetry.ReportStageAsync("retrieval.lexical", lexicalWatch.Elapsed.TotalMilliseconds, EmptyScores()).ConfigureAwait(false);
        await _telemetry.ReportStageAsync("retrieval.fusion", fusionWatch.Elapsed.TotalMilliseconds, EmptyScores()).ConfigureAwait(false);
        await _telemetry.ReportStageAsync("retrieval.resolve", resolveWatch.Elapsed.TotalMilliseconds, EmptyScores()).ConfigureAwait(false);
        await _telemetry.ReportStageAsync("retrieval.rerank", rerankWatch.Elapsed.TotalMilliseconds, EmptyScores()).ConfigureAwait(false);
        await _telemetry
            .ReportStageAsync("retrieval.fused", 0, new Dictionary<string, double>(StringComparer.Ordinal) { ["hits"] = fusedCount })
            .ConfigureAwait(false);
    }

    // Pure empty-scores factory; intentional static per architecture rule.
    private static IReadOnlyDictionary<string, double> EmptyScores() => new Dictionary<string, double>();
}