# ProvostXMemory.API.md

Types: 6  |  LOC: 859  |  ~832 tokens

---

Dependencies: ProvostXMemory.Abstractions, ProvostXMemory.Chunking, ProvostXMemory.Consolidation, ProvostXMemory.Correction, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Lexical, ProvostXMemory.Logging, ProvostXMemory.Reranking, ProvostXMemory.Retrieval, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex

### Interface: IMemoryService
> THE public surface of the memory library: hosts see only this plus options.
Methods:
  - Task RememberAsync(MemoryRecord record, CancellationToken cancellationToken = default)
  - Task ForgetAsync(MemoryScope scope, string id, CancellationToken cancellationToken = default)
  - Task<MemoryRecord?> GetAsync(MemoryScope scope, string id, CancellationToken cancellationToken = default)
  - Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken = default)
  - Task<RetrievalResult> RecallAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
  - Task ConsolidateAsync(MemoryScope scope, ConsolidationOptions options, CancellationToken cancellationToken = default)
  - Task BuildIndexesAsync(MemoryScope scope, CancellationToken cancellationToken = default)
  - Task RebuildAsync(CancellationToken cancellationToken = default)
  - IReadOnlyList<MemoryScope> Scopes()
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IScopesAware
> Pipeline wiring knobs layered over <see cref="MemoryServiceOptions"/>: which extra
Methods:
  - System.Collections.Generic.IReadOnlyList<MemoryScope> Scopes()
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Retrieval

### Class: GraphIndexLedger
> Index-time graph maintenance over the scope-pinned <see cref="IMemoryGraphStore"/>
Constructor:
  - GraphIndexLedger(IEntityExtractor extractor, Func<MemoryScope, IMemoryGraphStore> storeResolver, Func<MemoryScope, CancellationToken, Task<IReadOnlyList<MemoryRecord>>> recordsProvider)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Graph, ProvostXMemory.Consolidation

### Class: MemoryService
> Composition root: takes <see cref="MemoryServiceOptions"/> plus injected dependencies
Implements: IMemoryService
Constructor:
  - MemoryService(MemoryServiceOptions options, IMemoryStore store, IVectorEmbedder embedder, IVectorIndexFactory indexFactory, IResultFuser fuser, IMemoryTelemetry telemetry, IRetrievalGrader? grader = null, IEntityExtractor? graphExtractor = null, IReranker? reranker = null, IChunker? chunker = null, ChunkOptions? chunkOptions = null, MemoryServicePipelineOptions? pipelineOptions = null, Func<MemoryScope, IMemoryGraphStore>? graphStoreResolver = null, IMemoryLogSink? logSink = null)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Chunking, ProvostXMemory.Consolidation, ProvostXMemory.Correction, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Lexical, ProvostXMemory.Logging, ProvostXMemory.Retrieval, ProvostXMemory.Reranking, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex

### Class: MemoryServiceBuilder
> Fluent wiring helper for <see cref="MemoryService"/>. Collects the host's choices and
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Chunking, ProvostXMemory.Consolidation, ProvostXMemory.Correction, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Logging, ProvostXMemory.Retrieval, ProvostXMemory.Reranking, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex

### Class: RootChildScopeResolver
> Default plan-fan-out scope resolution: a target string names either the root scope
Implements: IScopeResolver
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Retrieval
