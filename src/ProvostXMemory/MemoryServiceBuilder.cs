using System;
using System.Collections.Generic;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Chunking;
using ProvostXMemory.Consolidation;
using ProvostXMemory.Correction;
using ProvostXMemory.Fusion;
using ProvostXMemory.Graph;
using ProvostXMemory.Logging;
using ProvostXMemory.Retrieval;
using ProvostXMemory.Reranking;
using ProvostXMemory.Storage;
using ProvostXMemory.Telemetry;
using ProvostXMemory.VectorIndex;

namespace ProvostXMemory;

/// <summary>
/// Fluent wiring helper for <see cref="MemoryService"/>. Collects the host's choices and
/// hands them to the <see cref="MemoryService"/> constructor — still plain constructor
/// injection underneath, no service locator, no ambient container.
/// </summary>
public sealed class MemoryServiceBuilder
{
    private MemoryServiceOptions? _options;
    private IMemoryStore? _store;
    private IVectorEmbedder? _embedder;
    private IVectorIndexFactory? _indexFactory;
    private IResultFuser? _fuser;
    private IMemoryTelemetry? _telemetry;
    private IRetrievalGrader? _grader;
    private IEntityExtractor? _graphExtractor;
    private Func<MemoryScope, IMemoryGraphStore>? _graphStoreResolver;
    private IReranker? _reranker;
    private IChunker? _chunker;
    private ChunkOptions? _chunkOptions;
    private MemoryServicePipelineOptions? _pipelineOptions;
    private IMemoryLogSink? _logSink;

    /// <summary>Sets the root options (required).</summary>
    public MemoryServiceBuilder WithOptions(MemoryServiceOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>Sets the store (required).</summary>
    public MemoryServiceBuilder WithStore(IMemoryStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        return this;
    }

    /// <summary>Sets the embedder (required).</summary>
    public MemoryServiceBuilder WithEmbedder(IVectorEmbedder embedder)
    {
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
        return this;
    }

    /// <summary>Sets the vector index factory (required).</summary>
    public MemoryServiceBuilder WithIndexFactory(IVectorIndexFactory indexFactory)
    {
        _indexFactory = indexFactory ?? throw new ArgumentNullException(nameof(indexFactory));
        return this;
    }

    /// <summary>Sets the result fuser (required).</summary>
    public MemoryServiceBuilder WithFuser(IResultFuser fuser)
    {
        _fuser = fuser ?? throw new ArgumentNullException(nameof(fuser));
        return this;
    }

    /// <summary>Sets the telemetry sink (required; NoopMemoryTelemetry.Instance for silence).</summary>
    public MemoryServiceBuilder WithTelemetry(IMemoryTelemetry telemetry)
    {
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        return this;
    }

    /// <summary>Sets the optional retrieval grader (enables corrective Recall).</summary>
    public MemoryServiceBuilder WithGrader(IRetrievalGrader grader)
    {
        _grader = grader ?? throw new ArgumentNullException(nameof(grader));
        return this;
    }

    /// <summary>Sets the optional entity extractor (enables graph indexing on Remember).</summary>
    public MemoryServiceBuilder WithGraphExtractor(IEntityExtractor extractor)
    {
        _graphExtractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        return this;
    }

    /// <summary>
    /// Sets the optional scope-pinned graph store resolver (with <see cref="WithGraphExtractor"/>
    /// enables graph indexing on Remember and cluster recording on Consolidate).
    /// </summary>
    public MemoryServiceBuilder WithGraphStoreResolver(Func<MemoryScope, IMemoryGraphStore> storeResolver)
    {
        _graphStoreResolver = storeResolver ?? throw new ArgumentNullException(nameof(storeResolver));
        return this;
    }

    /// <summary>Sets the optional reranker.</summary>
    public MemoryServiceBuilder WithReranker(IReranker reranker)
    {
        _reranker = reranker ?? throw new ArgumentNullException(nameof(reranker));
        return this;
    }

    /// <summary>Sets the optional chunker + chunk options (defaults when unset).</summary>
    public MemoryServiceBuilder WithChunker(IChunker chunker, ChunkOptions? chunkOptions = null)
    {
        _chunker = chunker ?? throw new ArgumentNullException(nameof(chunker));
        _chunkOptions = chunkOptions;
        return this;
    }

    /// <summary>Sets the pipeline options (defaults when unset).</summary>
    public MemoryServiceBuilder WithPipelineOptions(MemoryServicePipelineOptions pipelineOptions)
    {
        _pipelineOptions = pipelineOptions ?? throw new ArgumentNullException(nameof(pipelineOptions));
        return this;
    }

    /// <summary>
    /// Sets the optional injected log sink (used when <see cref="MemoryServiceOptions.Logging"/>
    /// selects <see cref="ProvostXMemory.Abstractions.MemoryLogSinkKind.Injected"/>).
    /// </summary>
    public MemoryServiceBuilder WithLogSink(IMemoryLogSink logSink)
    {
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        return this;
    }

    /// <summary>Builds the service; missing required dependencies throw with the missing member named.</summary>
    public MemoryService Build()
    {
        if (_options is null
            || _store is null
            || _embedder is null
            || _indexFactory is null
            || _fuser is null)
        {
            var missing = new List<string>();
            if (_options is null)
            {
                missing.Add(nameof(WithOptions));
            }

            if (_store is null)
            {
                missing.Add(nameof(WithStore));
            }

            if (_embedder is null)
            {
                missing.Add(nameof(WithEmbedder));
            }

            if (_indexFactory is null)
            {
                missing.Add(nameof(WithIndexFactory));
            }

            if (_fuser is null)
            {
                missing.Add(nameof(WithFuser));
            }

            throw new InvalidOperationException(
                $"MemoryService is missing required wiring: {string.Join(", ", missing)} (telemetry defaults to NoopMemoryTelemetry).");
        }

        return new MemoryService(
            _options,
            _store,
            _embedder,
            _indexFactory,
            _fuser,
            _telemetry ?? Telemetry.NoopMemoryTelemetry.Instance,
            _grader,
            _graphExtractor,
            _reranker,
            _chunker,
            _chunkOptions,
            _pipelineOptions,
            _graphStoreResolver,
            _logSink);
    }
}