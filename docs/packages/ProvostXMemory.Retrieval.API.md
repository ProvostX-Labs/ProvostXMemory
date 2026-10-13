# ProvostXMemory.Retrieval.API.md

Types: 15  |  LOC: 776  |  ~1105 tokens

---

Dependencies: ProvostXMemory.Abstractions, ProvostXMemory.Fusion, ProvostXMemory.Lexical, ProvostXMemory.Reranking, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex

### Interface: ICorrectiveRetriever
> Grader-gated hybrid retrieval with rewrite loop (corrective).
Methods:
  - Task<RetrievalResult> RetrieveAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IGraphRetrieverImpl
> Graph-backed retrieval (implementing package: Graph).
Methods:
  - Task<IReadOnlyList<ScoredMemoryRecord>> RetrieveAsync(string query, MemoryScope scope, int topK, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IHybridRetriever
> Dense + lexical retrieval in parallel → fused results.
Methods:
  - Task<RetrievalResult> RetrieveAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IMultiRepresentationRetriever
> Query across modalities (multi-representation records).
Methods:
  - Task<RetrievalResult> RetrieveAsync(RetrievalQuery query, MemoryRepresentationModality modality, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IQueryPlanRunner
> Executes a declarative <see cref="QueryPlan"/> (fan-out namespaces, merge, iterate).
Methods:
  - Task<RetrievalResult> RunAsync(QueryPlan plan, string text, MemoryScope scope, RetrievalFilters? filters = null, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IQueryTextRewriter
> Rewrites a query's plain text before a second retrieval pass (host-injected;
Methods:
  - Task<string> RewriteAsync(string text, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IScopeIndexRegistry
> Maps a <see cref="MemoryScope"/> to its per-scope retrieval artifacts. Implementations
Methods:
  - IVectorIndex GetVectorIndex(MemoryScope scope)
  - ILexicalIndex GetLexicalIndex(MemoryScope scope)
  - bool HasIndexes(MemoryScope scope)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Lexical, ProvostXMemory.VectorIndex

### Interface: IScopeResolver
> Executes a declarative <see cref="QueryPlan"/>. Each step fans out to its target
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Fusion

### Class: HybridRetriever
> Hybrid retrieval within one scope: dense (vector index) and lexical (BM25-lite index)
Implements: IHybridRetriever
Constructor:
  - HybridRetriever(IScopeIndexRegistry registry, IVectorEmbedder embedder, IResultFuser fuser, IMemoryTelemetry? telemetry = null, IReranker? reranker = null, FusionOptions? fusionOptions = null)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Fusion, ProvostXMemory.Lexical, ProvostXMemory.Reranking, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex

### Class: InMemoryScopeIndexRegistry
> Maps a <see cref="MemoryScope"/> to its per-scope retrieval artifacts. Implementations
Implements: IScopeIndexRegistry
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Lexical, ProvostXMemory.VectorIndex

### Class: KeywordQueryTextRewriter
> Zero-dependency <see cref="IQueryTextRewriter"/>: distills the query text to its
Implements: IQueryTextRewriter
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Lexical

### Class: MultiRepresentationRetriever
> Multi-representation retrieval: instead of scoring whole-record content, the query is
Implements: IMultiRepresentationRetriever
Constructor:
  - MultiRepresentationRetriever(IScopeIndexRegistry registry, IVectorEmbedder embedder)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex

### Class: QueryPlanRunner
> Executes a declarative <see cref="QueryPlan"/>. Each step fans out to its target
Implements: IQueryPlanRunner
Constructor:
  - QueryPlanRunner(IHybridRetriever retriever, IScopeIndexRegistry registry, IResultFuser fuser, IScopeResolver scopeResolver, IQueryTextRewriter? rewriter = null, QueryPlanRunnerOptions? options = null)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Fusion

### Class: QueryPlanRunnerOptions
> Executes a declarative <see cref="QueryPlan"/>. Each step fans out to its target
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Fusion

### Class: RetrievalFilterApplier
> Pure predicate that enforces the <see cref="RetrievalFilters"/> carried on a
Cross-package deps: ProvostXMemory.Abstractions
