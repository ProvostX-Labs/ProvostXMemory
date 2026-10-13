using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Chunking;
using ProvostXMemory.Consolidation;
using ProvostXMemory.Correction;
using ProvostXMemory.Fusion;
using ProvostXMemory.Graph;
using ProvostXMemory.Lexical;
using ProvostXMemory.Logging;
using ProvostXMemory.Retrieval;
using ProvostXMemory.Reranking;
using ProvostXMemory.Storage;
using ProvostXMemory.Telemetry;
using ProvostXMemory.VectorIndex;

namespace ProvostXMemory;

/// <summary>
/// Composition root: takes <see cref="MemoryServiceOptions"/> plus injected dependencies
/// and owns the configured retrieval pipeline. Remember/Forget/Update maintain the
/// per-scope indexes alongside the store; Recall routes through hybrid → optional
/// corrective → plan (when the query carries one) → optional rerank. Every store access
/// goes through the scope-bound <see cref="IMemoryStore"/> API, so cross-scope isolation
/// is structural. Per-scope indexes are created lazily and registered scope-exact.
/// </summary>
public sealed class MemoryService : IMemoryService
{
    /// <summary>Vector-id convention for a record's whole-content anchor embedding.</summary>
    public const string RecordAnchorPrefix = "record:";

    /// <summary>Vector-id convention for a record's chunk embedding.</summary>
    public const string ChunkPrefix = "chunk:";

    private readonly MemoryServiceOptions _options;
    private readonly IMemoryStore _store;
    private readonly IVectorEmbedder _embedder;
    private readonly IVectorIndexFactory _indexFactory;
    private readonly IResultFuser _fuser;
    private readonly IRetrievalGrader? _grader;
    private readonly IEntityExtractor? _graphExtractor;
    private readonly IMemoryTelemetry _telemetry;
    private readonly IReranker? _reranker;
    private readonly IChunker _chunker;
    private readonly ChunkOptions _chunkOptions;
    private readonly MemoryServicePipelineOptions _pipelineOptions;
    private readonly GraphIndexLedger? _graphLedger;
    private readonly MemoryLogger _logger;

    private readonly InMemoryScopeIndexRegistry _registry;
    private readonly HybridRetriever _hybridRetriever;
    private readonly MultiRepresentationRetriever _multiRepresentationRetriever;
    private readonly QueryPlanRunner _planRunner;
    private readonly ICorrectiveRetriever? _correctiveRetriever;
    private readonly object _indexLock = new();

    /// <summary>
    /// Dense-id ledger per scope: record id → indexed vector ids (chunks, anchor, reps).
    /// Gives exact incremental removal without id-schema probing; all access is guarded by the
    /// private index lock.
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, HashSet<string>>> _denseIdLedger = new(StringComparer.Ordinal);

    /// <summary>
    /// Serializes per-scope index MUTATIONS (lexical index is not internally thread-safe;
    /// engine backends are). Embedding computation (async, slow) stays outside this gate —
    /// only the index mutations themselves are serialized. Found by the 2026-10-14 audit:
    /// concurrent Remember/Update/Forget on one scope corrupted lexical postings.
    /// </summary>
    private readonly object _mutationGate = new();

    /// <summary>
    /// Serializes whole-record mutations (store write + index mutation + graph update)
    /// across ALL scopes on this service instance. Rationale (2026-10-14 audit follow-up):
    /// the store contract does not guarantee internal thread safety, and an unsynchronized
    /// store.Add racing on one scope loses records (proven by the parallel-remember
    /// regression test). Embedding computation stays OUTSIDE this gate (parallel, slow);
    /// only the mutation sequence holds it. Async gate (SemaphoreSlim) so store awaits
    /// are legal inside.
    /// </summary>
    private readonly SemaphoreSlim _storeGate = new(1, 1);

    public MemoryService(
        MemoryServiceOptions options,
        IMemoryStore store,
        IVectorEmbedder embedder,
        IVectorIndexFactory indexFactory,
        IResultFuser fuser,
        IMemoryTelemetry telemetry,
        IRetrievalGrader? grader = null,
        IEntityExtractor? graphExtractor = null,
        IReranker? reranker = null,
        IChunker? chunker = null,
        ChunkOptions? chunkOptions = null,
        MemoryServicePipelineOptions? pipelineOptions = null,
        Func<MemoryScope, IMemoryGraphStore>? graphStoreResolver = null,
        IMemoryLogSink? logSink = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(embedder);
        ArgumentNullException.ThrowIfNull(indexFactory);
        ArgumentNullException.ThrowIfNull(fuser);
        ArgumentNullException.ThrowIfNull(telemetry);
        ArgumentOutOfRangeException.ThrowIfZero(options.Vector.Dim);

        _options = options;
        _logger = new MemoryLogger(options.Logging ?? new MemoryLoggingOptions(), logSink);
        _store = store;
        _embedder = embedder;
        _indexFactory = indexFactory;
        _fuser = fuser;
        _telemetry = telemetry;
        _grader = grader;
        _graphExtractor = graphExtractor;
        _reranker = reranker;
        _chunker = chunker ?? new StructuredChunker();
        _chunkOptions = chunkOptions ?? new ChunkOptions();
        _pipelineOptions = pipelineOptions ?? new MemoryServicePipelineOptions();
        _graphLedger = graphExtractor is not null && graphStoreResolver is not null
            ? new GraphIndexLedger(
                graphExtractor,
                graphStoreResolver,
                (scope, token) => _store.SnapshotAsync(scope.Kind.ToString(), scope.Id, token))
            : null;

        _registry = new InMemoryScopeIndexRegistry();
        _hybridRetriever = new HybridRetriever(
            _registry,
            _embedder,
            _fuser,
            _telemetry,
            reranker,
            _options.Fusion);
        _multiRepresentationRetriever = new MultiRepresentationRetriever(_registry, _embedder);
        _planRunner = new QueryPlanRunner(
            _hybridRetriever,
            _registry,
            _fuser,
            new RootChildScopeResolver(),
            new KeywordQueryTextRewriter(),
            new QueryPlanRunner.QueryPlanRunnerOptions());
        _correctiveRetriever = grader is null || !_pipelineOptions.UseCorrectiveRecall
            ? null
            : new CorrectiveRetriever(
                _hybridRetriever,
                grader,
                new KeywordQueryRewriter(),
                options: new CorrectionOptions(_pipelineOptions.RelevanceFloor, _pipelineOptions.UseFetcherOnEmpty));

        WriteLog(MemoryLogLevel.Info, MemoryLogCategory.Lifecycle, "memory service built", scope: null);
    }

    /// <summary>The options this service was composed with.</summary>
    public MemoryServiceOptions Options => _options;

    public IRetrievalGrader? Grader => _grader;

    public IEntityExtractor? GraphExtractor => _graphExtractor;

    /// <summary>Scope-pinned registry behind the retrieval stages (diagnostics/testing surface).</summary>
    internal IScopeIndexRegistry Registry => _registry;

    /// <summary>The configured hybrid retriever (diagnostics/testing surface).</summary>
    internal IHybridRetriever HybridRetriever => _hybridRetriever;

    /// <inheritdoc />
    public async Task RememberAsync(MemoryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        // Embedding pass (slow, parallel-safe) strictly outside the mutation gates.
        var items = await BuildIndexItemsAsync(record, cancellationToken).ConfigureAwait(false);

        await _storeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                await _store.AddAsync(record, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LogStoreFailure(exception, record.Scope, "remember");
                throw;
            }

            ApplyIndexItems(record, items, cancellationToken);

            if (_pipelineOptions.ExtractGraphOnRemember && _graphLedger is not null)
            {
                await _graphLedger.ApplyAsync(record.Scope, record, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _storeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ForgetAsync(MemoryScope scope, string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();

        await _storeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                await _store.DeleteAsync(scope.Kind.ToString(), scope.Id, id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LogStoreFailure(exception, scope, "forget");
                throw;
            }

            RemoveFromIndexes(scope, id);
            _graphLedger?.DropScopeRecord(scope, id);
        }
        finally
        {
            _storeGate.Release();
        }
    }

    /// <inheritdoc />
    public Task<MemoryRecord?> GetAsync(MemoryScope scope, string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _store.GetAsync(scope.Kind.ToString(), scope.Id, id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        var items = await BuildIndexItemsAsync(record, cancellationToken).ConfigureAwait(false);

        await _storeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                await _store.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LogStoreFailure(exception, record.Scope, "update");
                throw;
            }

            RemoveFromIndexes(record.Scope, record.Id);
            ApplyIndexItems(record, items, cancellationToken);
            _graphLedger?.DropScopeRecord(record.Scope, record.Id);
            if (_pipelineOptions.ExtractGraphOnRemember && _graphLedger is not null)
            {
                await _graphLedger.ApplyAsync(record.Scope, record, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _storeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<RetrievalResult> RecallAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (query.Plan is { } plan)
        {
            return await _planRunner.RunAsync(plan, query.Text, query.Scope, query.Filters, cancellationToken).ConfigureAwait(false);
        }

        // NOTE: no facade-level rerank — the hybrid retriever (and the corrective
        // retriever wrapping it) already reranks internally with the SAME injected
        // reranker; a second pass here would compound score boosts (double rerank,
        // found by the 2026-10-14 second audit).
        return _correctiveRetriever is not null
            ? await _correctiveRetriever.RetrieveAsync(query, cancellationToken).ConfigureAwait(false)
            : await _hybridRetriever.RetrieveAsync(query, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ConsolidateAsync(MemoryScope scope, ConsolidationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var before = new HashSet<string>(
            (await _store.ListAsync(scope.Kind.ToString(), scope.Id, cancellationToken).ConfigureAwait(false)).Select(record => record.Id),
            StringComparer.Ordinal);

        var consolidator = new MemoryConsolidator(
            _store,
            new ThresholdDedupDetector(_embedder),
            _embedder,
            new ClusterSummarizer());
        IReadOnlyList<MemoryRecord> result;
        try
        {
            result = await consolidator
                .ConsolidateAsync(scope.Kind.ToString(), scope.Id, options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogStoreFailure(exception, scope, "consolidate");
            throw;
        }

        WriteLog(
            MemoryLogLevel.Info,
            MemoryLogCategory.Consolidation,
            $"consolidation kept {result.Count}, removed {before.Count - ResultIds(result).Count} of {before.Count}",
            scope);

        // Index refresh: survivors whose tags/content were merged during dedup, plus
        // every newly created summary record.
        var refreshed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in result)
        {
            if (refreshed.Add(record.Id))
            {
                RemoveFromIndexes(record.Scope, record.Id);
                await IndexRecordAsync(record, cancellationToken).ConfigureAwait(false);
            }
        }

        // Cluster side: IClusterSummarizer-produced summaries are recorded as graph clusters
        // linking their member entities (scope-bound through the ledger's store resolver);
        // inert when no graph ledger is wired.
        if (_pipelineOptions.RecordClustersOnConsolidate && _graphLedger is not null)
        {
            foreach (var record in result)
            {
                if (GraphIndexLedger.IsClusterSummary(record))
                {
                    await _graphLedger.RecordClusterSummaryAsync(record, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // Records deleted by consolidation (duplicates, TTL-expired) leave stale dense
        // and lexical entries behind unless explicitly removed.
        foreach (var removedId in before.Except(result.Select(record => record.Id)))
        {
            RemoveFromIndexes(scope, removedId);
        }
    }

    /// <inheritdoc />
    public async Task BuildIndexesAsync(MemoryScope scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();
        var records = await _store.SnapshotAsync(scope.Kind.ToString(), scope.Id, cancellationToken).ConfigureAwait(false);
        await BuildIndexesCoreAsync(scope, records, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RebuildAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _registry.Reset();
        lock (_indexLock)
        {
            _denseIdLedger.Clear();
        }

        foreach (var scope in Scopes())
        {
            var records = await _store.SnapshotAsync(scope.Kind.ToString(), scope.Id, cancellationToken).ConfigureAwait(false);
            await BuildIndexesCoreAsync(scope, records, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<MemoryScope> Scopes()
    {
        if (_store is IScopesAware aware)
        {
            return aware.Scopes();
        }

        return DirectoryScopes();
    }

    /// <summary>
    /// Scope enumeration fallback for stores that are not <see cref="IScopesAware"/>:
    /// reads scope directory names under the configured root
    /// (<c>&lt;root&gt;/&lt;scope-kind&gt;/&lt;scope-id&gt;</c> layout). Empty when the
    /// root does not exist yet.
    /// </summary>
    private IReadOnlyList<MemoryScope> DirectoryScopes()
    {
        var rootPath = _options.Storage.RootPath;
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return Array.Empty<MemoryScope>();
        }

        var scopes = new List<MemoryScope>();
        foreach (var kindDir in Directory.EnumerateDirectories(rootPath))
        {
            if (!Enum.TryParse<MemoryScopeKind>(Path.GetFileName(kindDir), ignoreCase: true, out var kind))
            {
                continue;
            }

            foreach (var idDir in Directory.EnumerateDirectories(kindDir))
            {
                scopes.Add(new MemoryScope(kind, Path.GetFileName(idDir)));
            }
        }

        return scopes
            .OrderBy(scope => scope.Kind)
            .ThenBy(scope => scope.Id, StringComparer.Ordinal)
            .ToList();
    }

    private async Task BuildIndexesCoreAsync(MemoryScope scope, IReadOnlyList<MemoryRecord> records, CancellationToken cancellationToken)
    {
        var vector = await RebuildVectorIndexAsync(records, cancellationToken).ConfigureAwait(false);
        var lexical = new LexicalIndex(new Bm25Scorer());
        lexical.Build(LexicalDocuments(records));

        _registry.RegisterVectorIndex(scope, vector);
        _registry.RegisterLexicalIndex(scope, lexical);
        _registry.RegisterRecordProvider(
            scope,
            (targetScope, token) => targetScope.Kind == scope.Kind && targetScope.Id == scope.Id
                ? _store.SnapshotAsync(scope.Kind.ToString(), scope.Id, token)
                : Task.FromResult<IReadOnlyList<MemoryRecord>>(Array.Empty<MemoryRecord>()));

        lock (_indexLock)
        {
            _denseIdLedger[ScopeKey(scope)] = BuildLedger(records);
        }

        WriteLog(MemoryLogLevel.Info, MemoryLogCategory.IndexBuild, $"index build: {records.Count} records", scope);
    }

    private async Task<IVectorIndex> RebuildVectorIndexAsync(IReadOnlyList<MemoryRecord> records, CancellationToken cancellationToken)
    {
        var vector = _indexFactory.Create(_options.Vector.IndexKind, _options.Vector.Dim);
        var items = new List<VectorItem>();
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunks = _chunker.Split(record, _chunkOptions);
            foreach (var chunk in chunks)
            {
                items.Add(new VectorItem(
                    $"chunk:{chunk.ChunkId}",
                    await _embedder.EmbedAsync(chunk.Content, cancellationToken).ConfigureAwait(false)));
            }

            items.Add(new VectorItem(
                $"record:{record.Id}",
                await _embedder.EmbedAsync(record.Content, cancellationToken).ConfigureAwait(false)));

            for (var i = 0; i < record.Reps.Count; i++)
            {
                items.Add(new VectorItem(
                    MultiRepresentationRetriever.RepresentationId(record.Id, i),
                    await _embedder.EmbedAsync(record.Reps[i].Content, cancellationToken).ConfigureAwait(false)));
            }
        }

        await vector.BuildAsync(items, cancellationToken).ConfigureAwait(false);
        await vector.FlushAsync(cancellationToken).ConfigureAwait(false);
        return vector;
    }

    private async Task IndexRecordAsync(MemoryRecord record, CancellationToken cancellationToken)
    {
        var items = await BuildIndexItemsAsync(record, cancellationToken).ConfigureAwait(false);
        ApplyIndexItems(record, items, cancellationToken);
    }

    /// <summary>Embedding pass (async, potentially slow, parallel-safe) — outside all gates.</summary>
    private async Task<List<VectorItem>> BuildIndexItemsAsync(MemoryRecord record, CancellationToken cancellationToken)
    {
        var items = new List<VectorItem>();
        foreach (var chunk in _chunker.Split(record, _chunkOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Add(new VectorItem(
                $"chunk:{chunk.ChunkId}",
                await _embedder.EmbedAsync(chunk.Content, cancellationToken).ConfigureAwait(false)));
        }

        items.Add(new VectorItem(
            $"record:{record.Id}",
            await _embedder.EmbedAsync(record.Content, cancellationToken).ConfigureAwait(false)));

        for (var i = 0; i < record.Reps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Add(new VectorItem(
                MultiRepresentationRetriever.RepresentationId(record.Id, i),
                await _embedder.EmbedAsync(record.Reps[i].Content, cancellationToken).ConfigureAwait(false)));
        }

        return items;
    }

    /// <summary>
    /// Mutation pass — serialized by the store gate (callers) plus this index gate: the
    /// lexical index (and FlatJson vector backend) are not internally thread-safe.
    /// Blocking on the engine's Task-based Add is safe: the engine locks internally and
    /// this gate is only ever held by mutation threads, never by embedding/IO work.
    /// </summary>
    private void ApplyIndexItems(MemoryRecord record, List<VectorItem> items, CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(items.Select(item => item.Id), StringComparer.Ordinal);
        var document = LexicalDocuments(new[] { record })[0];

        lock (_mutationGate)
        {
            var (vector, lexical) = EnsureScopeIndexes(record.Scope);
            foreach (var item in items)
            {
                vector.AddAsync(item, cancellationToken).GetAwaiter().GetResult();
            }

            vector.FlushAsync(cancellationToken).GetAwaiter().GetResult();
            lexical.Remove(record.Id);
            lexical.IncrementalAdd(document);

            lock (_indexLock)
            {
                LedgerOf(record.Scope)[record.Id] = ids;
            }
        }
    }

    private void RemoveFromIndexes(MemoryScope scope, string recordId)
    {
        // Serialized with IndexRecordAsync mutations (same gate): the lexical index
        // rebuild on Remove is not internally thread-safe.
        lock (_mutationGate)
        {
            if (!_registry.HasIndexes(scope))
            {
                return;
            }

            string[] ids;
            lock (_indexLock)
            {
                ids = _denseIdLedger.TryGetValue(ScopeKey(scope), out var ledger)
                      && ledger.TryGetValue(recordId, out var recorded)
                    ? recorded.ToArray()
                    : Array.Empty<string>();
            }

            if (ids.Length > 0)
            {
                var vector = _registry.GetVectorIndex(scope);
                foreach (var id in ids)
                {
                    TryRemoveVector(vector, id);
                }
            }

            _registry.GetLexicalIndex(scope).Remove(recordId);

            lock (_indexLock)
            {
                if (_denseIdLedger.TryGetValue(ScopeKey(scope), out var ledger))
                {
                    ledger.Remove(recordId);
                }
            }
        }
    }

    private void TryRemoveVector(IVectorIndex vector, string id)
    {
        try
        {
            vector.RemoveAsync(id).GetAwaiter().GetResult();
        }
        catch (VectorIndexIdNotFoundException)
        {
            // Absent ids (already removed, rebuild replaced the index instance) are fine.
        }
    }

    private static IReadOnlyList<LexicalDocument> LexicalDocuments(IReadOnlyList<MemoryRecord> records)
    {
        var documents = new List<LexicalDocument>(records.Count);
        foreach (var record in records)
        {
            var keyTerms = new List<string>(record.Tags.Count + 1) { record.Key };
            keyTerms.AddRange(record.Tags);
            documents.Add(new LexicalDocument(
                record.Id,
                LexicalTokenizer.Tokenize(record.Content),
                keyTerms,
                record.Category));
        }

        return documents;
    }

    private (IVectorIndex Vector, ILexicalIndex Lexical) EnsureScopeIndexes(MemoryScope scope)
    {
        lock (_indexLock)
        {
            if (_registry.HasIndexes(scope))
            {
                return (_registry.GetVectorIndex(scope), _registry.GetLexicalIndex(scope));
            }

            var vector = _indexFactory.Create(_options.Vector.IndexKind, _options.Vector.Dim);
            var lexical = new LexicalIndex(new Bm25Scorer());
            WriteLog(MemoryLogLevel.Info, MemoryLogCategory.Lifecycle, "scope indexes opened", scope);
            _registry.RegisterVectorIndex(scope, vector);
            _registry.RegisterLexicalIndex(scope, lexical);
            _registry.RegisterRecordProvider(
                scope,
                (targetScope, token) => targetScope.Kind == scope.Kind && targetScope.Id == scope.Id
                    ? _store.SnapshotAsync(scope.Kind.ToString(), scope.Id, token)
                    : Task.FromResult<IReadOnlyList<MemoryRecord>>(Array.Empty<MemoryRecord>()));
            return (vector, lexical);
        }
    }

    private Dictionary<string, HashSet<string>> LedgerOf(MemoryScope scope)
    {
        var key = ScopeKey(scope);
        if (!_denseIdLedger.TryGetValue(key, out var ledger))
        {
            ledger = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            _denseIdLedger[key] = ledger;
        }

        return ledger;
    }

    private static Dictionary<string, HashSet<string>> BuildLedger(IReadOnlyList<MemoryRecord> records)
    {
        var ledger = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal)
            {
                $"record:{record.Id}",
            };
            for (var i = 0; i < record.Reps.Count; i++)
            {
                ids.Add(MultiRepresentationRetriever.RepresentationId(record.Id, i));
            }

            ledger[record.Id] = ids;
        }

        return ledger;
    }

    // Pure scope → ledger key mapping; intentional static per architecture rule.
    private static string ScopeKey(MemoryScope scope) => $"{scope.Kind}/{scope.Id}";

    private static IReadOnlySet<string> ResultIds(IReadOnlyList<MemoryRecord> records)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            ids.Add(record.Id);
        }

        return ids;
    }

    /// <summary>
    /// Emits a log entry when logging is enabled; disabled logging allocates nothing here.
    /// Message strings are operational metadata only — record content is never included.
    /// </summary>
    private void WriteLog(MemoryLogLevel level, MemoryLogCategory category, string message, MemoryScope? scope)
    {
        if (_logger.IsEnabled)
        {
            _logger.Log(
                level,
                category,
                message,
                scope: scope is null ? null : new MemoryLogScope(scope.Kind.ToString(), scope.Id));
        }
    }

    /// <summary>Classifies a store failure: missing scope/record ids are scope rejections (warn), everything else is a store error (error level).</summary>
    private void LogStoreFailure(Exception exception, MemoryScope scope, string operation)
    {
        var isScopeRejection = exception is KeyNotFoundException;
        WriteLog(
            isScopeRejection ? MemoryLogLevel.Warn : MemoryLogLevel.Error,
            isScopeRejection ? MemoryLogCategory.ScopeRejection : MemoryLogCategory.StoreError,
            $"{operation} failed: {exception.GetType().Name}",
            scope);
    }
}