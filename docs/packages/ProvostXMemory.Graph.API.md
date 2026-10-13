# ProvostXMemory.Graph.API.md

Types: 11  |  LOC: 1022  |  ~979 tokens

---

Dependencies: ProvostXMemory.Abstractions, ProvostXMemory.Storage

### Interface: IEntityExtractor
> Host-injected entity/relation extractor (LLM or heuristic).
Methods:
  - Task<ExtractionResult> ExtractAsync(IReadOnlyList<MemoryRecord> records, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IGraphRetriever
> Retrieves records from a relevant subgraph plus cluster summaries.
Methods:
  - Task<IReadOnlyList<ScoredMemoryRecord>> RetrieveAsync(string query, MemoryScope scope, int topK, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IMemoryGraphStore
> Graph-backed store over memory records: entities, relations, paths, clusters.
Methods:
  - Task UpsertEntityAsync(GraphEntity entity, CancellationToken cancellationToken = default)
  - Task UpsertRelationAsync(GraphRelation relation, CancellationToken cancellationToken = default)
  - Task UpsertClusterAsync(GraphCluster cluster, CancellationToken cancellationToken = default)
  - Task<GraphEntity?> GetEntityAsync(string entityId, CancellationToken cancellationToken = default)
  - Task<IReadOnlyList<GraphEntity>> SearchEntitiesAsync(string query, int maxResults, CancellationToken cancellationToken = default)
  - Task<IReadOnlyList<GraphRelation>> RelationsOfAsync(string entityId, int maxRelations, CancellationToken cancellationToken = default)
  - Task<IReadOnlyList<GraphEntity>> NeighborsAsync(string entityId, int maxNeighbors, CancellationToken cancellationToken = default)
  - Task<IReadOnlyList<GraphEntity>> ShortestPathAsync(string fromEntityId, string toEntityId, CancellationToken cancellationToken = default)
  - Task<GraphCluster?> ClusterOfAsync(string entityId, CancellationToken cancellationToken = default)

### Interface: IMemoryGraphStoreResolver
> Resolves the scope-pinned graph store for a memory scope. Implementations must
Methods:
  - IMemoryGraphStore Resolve(MemoryScope scope)
Cross-package deps: ProvostXMemory.Abstractions

### Class: GraphRetriever
> Graph-backed retriever: lowercased query tokens are matched against entity ids and
Implements: IGraphRetriever
Constructor:
  - GraphRetriever(Func<MemoryScope, IMemoryGraphStore> storeFactory, Func<MemoryScope, CancellationToken, Task<IReadOnlyList<MemoryRecord>>> recordsProvider, GraphOptions? options = null)
Cross-package deps: ProvostXMemory.Abstractions

### Class: HeuristicEntityExtractor
> Zero-dependency heuristic extractor: capitalized multi-word phrases and keyword/tag
Implements: IEntityExtractor
Constructor:
  - HeuristicEntityExtractor(IReadOnlyList<string>? keywords = null)
Cross-package deps: ProvostXMemory.Abstractions

### Class: JsonlMemoryGraphStore
> JSONL-backed graph store: <c>graph/entities.jsonl</c>, <c>graph/relations.jsonl</c> and
Implements: IMemoryGraphStore
Constructor:
  - JsonlMemoryGraphStore(MemoryRootLayout layout, string scopeKind, string scopeId, AtomicFileWriter? writer = null, MemoryRootLayout layout, MemoryScope scope, AtomicFileWriter? writer = null)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Storage

### Record: ExtractionResult
> Entity + relation extraction result.
Constructor:
  - ExtractionResult(IReadOnlyList<GraphEntity> Entities, IReadOnlyList<GraphRelation> Relations)

### Record: GraphCluster
> A graph cluster (community) with an optional summary record id.
Constructor:
  - GraphCluster(string ClusterId, IReadOnlyList<string> EntityIds, string? SummaryRecordId)

### Record: GraphEntity
> An extracted graph entity. <see cref="EntityId"/> is the stable slug id;
Constructor:
  - GraphEntity(string EntityId, string Name, IReadOnlyList<string> Aliases, IReadOnlyList<string> RecordIds)

### Record: GraphRelation
> A relation between two graph entities, treated as undirected for traversal.
Constructor:
  - GraphRelation(string RelationId, string FromEntityId, string ToEntityId, string Label, IReadOnlyList<string> RecordIds)
