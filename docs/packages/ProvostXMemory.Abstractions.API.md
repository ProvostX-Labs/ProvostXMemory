# ProvostXMemory.Abstractions.API.md

Types: 16  |  LOC: 364  |  ~1119 tokens

---

Dependencies: ProvostXMemory.Logging

### Interface: IJsonlMemoryStore
> Default JSONL-backed store contract (transactional file I/O).
Implements: IMemoryStore
Methods:
  - Task<MemoryEntryModel?> ReadEntryAsync(string scopeKind, string scopeId, string id, CancellationToken cancellationToken = default)
  - Task WriteEntryAsync(string scopeKind, string scopeId, MemoryEntryModel entry, CancellationToken cancellationToken = default)

### Interface: IMemoryManifestCodec
> Manifest read/write for a memory root.
Methods:
  - Task<MemoryManifest> ReadAsync(string rootPath, CancellationToken cancellationToken = default)
  - Task WriteAsync(string rootPath, MemoryManifest manifest, CancellationToken cancellationToken = default)

### Interface: IMemoryStore
> Scope-bound CRUD + listing over memory records.
Methods:
  - Task AddAsync(MemoryRecord record, CancellationToken cancellationToken = default)
  - Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken = default)
  - Task DeleteAsync(string scopeKind, string scopeId, string id, CancellationToken cancellationToken = default)
  - Task<MemoryRecord?> GetAsync(string scopeKind, string scopeId, string id, CancellationToken cancellationToken = default)
  - Task<IReadOnlyList<MemoryRecord>> ListAsync(string scopeKind, string scopeId, CancellationToken cancellationToken = default)
  - Task<long> CountAsync(string scopeKind, string scopeId, CancellationToken cancellationToken = default)
  - Task<IReadOnlyList<MemoryRecord>> SnapshotAsync(string scopeKind, string scopeId, CancellationToken cancellationToken = default)

### Class: QueryTokens
> Pure query-text token helpers shared by text-processing packages (keyword rewriting,

### Record: BackendStats
> Stats snapshot of a backend (store or index).
Constructor:
  - BackendStats(long Count, long BytesOnDisk, DateTimeOffset? LastFlushedAt = null)

### Record: ChunkOptions
> Chunking parameters: structural markers, size and overlap. A trailing chunk shorter
Constructor:
  - ChunkOptions(int MaxChunkLength = 512, int Overlap = 0, bool RespectStructure = true, string? Separator = null, int MinChunkLength = 0)

### Record: ConsolidationOptions
> Consolidation pass options: dedup threshold, TTL, summaries.
Constructor:
  - ConsolidationOptions(double DedupSimilarityThreshold = 0.92, bool ExpireTtl = true, bool EmitClusterSummaries = false)

### Record: FusionOptions
> Fusion options: RRF constant and stage weights.
Constructor:
  - FusionOptions(double RrfK = 60.0, double DenseWeight = 1.0, double LexicalWeight = 1.0)

### Record: MemoryEntryModel
> Logical record model as persisted on disk (JSONL entry shape). <see cref="Reps"/> and
Constructor:
  - MemoryEntryModel(string Id, string ScopeKind, string ScopeId, string Key, string Content, string? Category, IReadOnlyList<string> Tags, long CreatedAtUnixMs, long UpdatedAtUnixMs, long? ExpiresAtUnixMs, IReadOnlyList<MemoryRepresentation>? Reps = null, IReadOnlyDictionary<string, string>? Meta = null)

### Record: MemoryManifest
> On-disk metadata describing the memory root: codecs, counts, versions.
Constructor:
  - MemoryManifest(string Version, long EntryCount, long UpdatedAtUnixMs)

### Record: MemoryServiceOptions
> Root options for the memory service; injected by the host.
Constructor:
  - MemoryServiceOptions(StorageOptions Storage, VectorOptions Vector, GraphOptions Graph, FusionOptions Fusion, ConsolidationOptions Consolidation, MemoryLoggingOptions? Logging = null)
Cross-package deps: ProvostXMemory.Logging

### Record: RetrievalQuery
> A scope-bound retrieval query. Immutable.
Constructor:
  - RetrievalQuery(string Text, MemoryScope Scope, int TopK = 10, RetrievalFilters? Filters = null, QueryPlan? Plan = null)

### Record: ScoredId
> A scored hit from a store/index search: scope kind, scope id, id, score.
Constructor:
  - ScoredId(string ScopeKind, string ScopeId, string Id, double Score)

### Record: ScoredMemoryRecord
> A memory record together with a retrieval score and provenance.
Constructor:
  - ScoredMemoryRecord(MemoryRecord Record, double Score, RetrievalProvenance Provenance)

### Record: StorageOptions
> FlatJson store options: root folder + durability settings.
Constructor:
  - StorageOptions(string RootPath, bool AtomicWrites = true, bool Fsync = false)

### Record: VectorOptions
> Vector index options: backend kind, model hash, dimension, default top-k.
Constructor:
  - VectorOptions(string EmbeddingModelHash, int Dim, IndexKind IndexKind = IndexKind.Flat, int DefaultTopK = 10)
