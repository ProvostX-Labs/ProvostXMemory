# API Glossary

Reference for the public types of ProvostXMemory, grouped by namespace. Hosts normally interact only with the root facade, the abstractions, and the backend factories; the remaining namespaces document the contracts behind each capability.

Legend: interfaces are marked **I**, classes **C**, records **R**, enums **E**.

---

## ProvostXMemory (facade)

### IMemoryService (I)

The single public surface of the library. Every operation is scope-bound.

| Member | Description |
|---|---|
| `RememberAsync(record, ct?)` | Stores a record and maintains dense/lexical indexes (and the optional graph ledger). |
| `ForgetAsync(scope, id, ct?)` | Deletes a record and its index artifacts in that scope. |
| `GetAsync(scope, id, ct?)` | Returns the record or `null`; scope-exact. |
| `UpdateAsync(record, ct?)` | Updates record content/metadata and refreshes index entries. |
| `RecallAsync(query, ct?)` | Runs the retrieval pipeline; routes plan → corrective → hybrid, then optional rerank. |
| `ConsolidateAsync(scope, options, ct?)` | Runs a consolidation pass and refreshes index artifacts. |
| `BuildIndexesAsync(scope, ct?)` | Chunks, embeds, and builds vector + lexical indexes for a scope. |
| `RebuildAsync(ct?)` | Rebuilds indexes for all known scopes (deterministic; identical rankings). |
| `Scopes()` | Enumerates scopes known under the root. |

**Usage note:** this is the only type most hosts need beyond options and factories. See [Getting Started](getting-started.md).

### MemoryService (C)

Composition root implementing `IMemoryService`. Constructor takes `MemoryServiceOptions` plus injected dependencies: store, embedder, index factory, fuser, telemetry, and optional grader, graph extractor, reranker, chunker (+ `ChunkOptions`), pipeline options, graph-store resolver, and log sink. Not typically constructed directly — use the builder.

### MemoryServiceBuilder (C)

Fluent wiring helper; plain constructor injection underneath (no service locator).

| Member | Description |
|---|---|
| `WithOptions(options)` | Root options (required). |
| `WithStore(store)` | `IMemoryStore` implementation (required). |
| `WithEmbedder(embedder)` | Host embedding model (required). |
| `WithIndexFactory(factory)` | Backend factory (required). |
| `WithFuser(fuser)` | Result fuser (required). |
| `WithTelemetry(telemetry)` | Telemetry sink (required; `NoopMemoryTelemetry.Instance` for silence). |
| `WithGrader(grader)` | Optional; enables the corrective recall route. |
| `WithGraphExtractor(...)` / graph-store resolver | Optional graph ingestion. |
| `WithReranker(reranker)` | Optional post-fusion reranking. |
| `WithChunker(chunker, options)` | Optional custom chunking. |
| `WithLogSink(sink)` | Optional injected log sink. |
| `Build()` | Validates all required wiring and returns `IMemoryService`. |

### IScopesAware (I)

Optional store hook: `Scopes()` returns all scopes currently known to the store. Enables `IMemoryService.Scopes()`; the facade falls back to the directory layout when the store does not implement it.

### GraphIndexLedger (C)

Keeps the scope's graph store in sync with record mutations (entity upsert/removal driven by the injected `IEntityExtractor`, including consolidation cluster writes).

---

## ProvostXMemory.Abstractions

Core contracts and immutable data types shared by every layer.

### MemoryRecord (R)

Immutable memory unit. Members: `Id`, `Scope`, `Key`, `Content`, `Category?`, `Tags`, `Reps`, `CreatedAt`, `UpdatedAt`, `ExpiresAt?`, `Meta`. See [Concepts](concepts.md#records).

### MemoryScope (R) and MemoryScopeKind (E)

Hierarchical isolation boundary: `(Kind, Id, ParentScope?)` with kinds `Platform | Session | Tenant | Purpose | Custom`. Members: `PathSegment`, `Chain()`, `Contains(other)`, `WithParent(parent)`. See [Scopes](scopes.md).

### MemoryRepresentation (R), MemoryRepresentationModality (E)

One modality-specific representation of a record: `Modality` (`Text | Image | Table | Audio | Custom`), `Content`, optional `EmbeddingModelHash`, `Properties` metadata.

### RetrievalQuery (R)

`Text`, `Scope`, `TopK` (default set by `VectorOptions.DefaultTopK`), `Filters?`, `Plan?`.

### RetrievalFilters (R)

`Tags`, `Category?`, `From?`, `To?` — enforced after candidate resolution.

### RetrievalResult (R)

`Records` (`IReadOnlyList<ScoredMemoryRecord>`) and `Diagnostics` (`IReadOnlyList<RetrievalStageDiagnostics>`: stage name, duration, per-stage scores).

### ScoredMemoryRecord (R)

`Record`, `Score`, `Provenance` (`RetrievalProvenance`: retriever name + stage scores).

### QueryPlan (R), QueryPlanStep (R)

Declarative retrieval plan: `PlanId` + ordered steps; each step has `StepId`, `Action`, `TargetScopes`, `TopK`.

### IMemoryStore (I)

Scope-bound CRUD over records.

| Member | Description |
|---|---|
| `AddAsync` / `UpdateAsync` | Persist a record in its scope. |
| `DeleteAsync(scopeKind, scopeId, id)` | Remove one record. |
| `GetAsync(scopeKind, scopeId, id)` | Scope-exact fetch; `null` when absent. |
| `ListAsync(scopeKind, scopeId)` | All records of the scope. |
| `CountAsync(scopeKind, scopeId)` | Record count. |
| `SnapshotAsync(scopeKind, scopeId)` | Consistent snapshot for index builds. |

**Usage note:** hosts rarely implement this; use `JsonlMemoryStore`. Custom implementations must remain scope-exact.

### IJsonlMemoryStore (I)

Contract marker for the default JSONL store implementation (`ProvostXMemory.Storage`).

### IMemoryManifestCodec (I)

Reads/writes the root `manifest.json` (`MemoryManifest`: schema id, counts, timestamps; row model `MemoryEntryModel` persists records with backward-compatible Reps/Meta).

### Options records

| Type | Members (defaults) |
|---|---|
| `MemoryServiceOptions` (R) | `Storage`, `Vector`, `Graph`, `Fusion`, `Consolidation`, optional `Logging`. |
| `StorageOptions` (R) | `Root`, `AtomicWrites = true`, `Fsync`. |
| `VectorOptions` (R) | `ModelHash`, `Dim`, `IndexKind = Flat`, `DefaultTopK`. |
| `GraphOptions` (R) | `Enabled = true`, `MaxNeighbors = 16`, `MaxHops = 2`, `MaxClusters = 5`; `GraphOptions.Default`. |
| `FusionOptions` (R) | `RrfK = 60`, `DenseWeight`, `LexicalWeight`. |
| `ConsolidationOptions` (R) | `DedupSimilarityThreshold`, `ExpireTtl`, `EmitClusterSummaries`. |
| `ChunkOptions` (R) | `MaxChunkLength = 512`, `Overlap`, `RespectStructure`, `Separator?`, `MinChunkLength`. |

### Supporting types

`BackendStats` (R) — item count, dimension stats, `LastFlushedAt`; `ScoredId` (R) — scope-qualified id + score used by fusion; `QueryTokens` (C) — query tokenization helper.

---

## ProvostXMemory.Storage

### MemoryRootLayout (C)

Resolves root + scope paths (`root/scopeKind/scopeId/modelHash`); the structural basis of scope isolation. Constructor takes the root path and the model hash.

### JsonlMemoryStore (C) : IJsonlMemoryStore

Default store: one JSONL file per scope (`records.jsonl`), transactional writes.

### AtomicFileWriter (C)

Temp-file + atomic-swap writer with optional fsync; all durable file writes flow through it.

### MemoryManifestCodec (C) : IMemoryManifestCodec

Manifest (de)serialization.

---

## ProvostXMemory.VectorIndex

### IVectorIndex (I)

| Member | Description |
|---|---|
| `Dim`, `Count` | Dimension and item count. |
| `BuildAsync(items)` | Replace contents with the given `VectorItem` set. |
| `AddAsync(item)` / `RemoveAsync(id)` | Incremental mutation. |
| `SearchAsync(vector, k)` | Top-k search → `ScoredVectorId` list. |
| `FlushAsync` | Persist durable state. |

**Usage note:** all backends implement this and pass the same conformance suite.

### IVectorIndexFactory (I)

`Create(kind, dim)` returns an index of `IndexKind` (`Flat | Ivf | Hnsw | Gpu`).

### IVectorEmbedder (I)

Host-injected embedder: `ModelHash`, `Dim`, `EmbedAsync(text)`. The library validates hash/dimension but never chooses models.

### IVectorIndexGuard (I)

Model-hash/dimension guard consulted when opening persisted index artifacts.

### VectorNamespaceFile (C)

Per-scope/model-hash index file format (FlatJson backend persistence).

### Exceptions

`VectorIndexGuardMismatchException` — model hash or dimension mismatch on open. `VectorIndexIdNotFoundException` — unknown vector id requested for removal/resolution.

### Supporting types

`VectorItem` (R) — id + vector; `ScoredVectorId` (R) — id + score; `ScalarVectorOps` (C) — scalar distance helpers.

---

## ProvostXMemory.VectorIndex.Engine

Own C# vector engine; zero third-party **vector** dependencies (ILGPU — the GPU compiler for the included kernels, NCSA license — is the sole non-vector dependency and falls back to the CPU bit-identically). Namespace name reflects history; the FAISS dependency was replaced by this engine.

### EngineVectorIndexFactory (C) : IVectorIndexFactory

Creates `FlatExactIndex`, `IvfFlatIndex`, or `HnswIndex`; rejects `IndexKind.Gpu` (explicit GPU routing only).

### FlatExactIndex (C) : IVectorIndex

Exact brute-force search with SIMD (`Vector512`) kernels.

### IvfFlatIndex (C) : IVectorIndex

k-means coarse quantizer + inverted posting lists; `nprobe`-style partial scan. Supporting: `KMeansQuantizer`, `KMeansResult`, `IvfPostingLists`, `IvfMember`.

### HnswIndex (C) : IVectorIndex

Hierarchical navigable small-world graph. Immutable after build; mutations trigger batch rebuild (patent-safe semantics). Supporting: `HnswBuilder`, `HnswGraph`, `HnswSearcher`, `HnswBeamSearcher`, `HnswSearchContext`, `HnswLevelAssigner`, `HnswPublishedGraph`, `HnswOptions` (seeded, deterministic builds).

### Options (C) — engine options

Three overloads configure the metric (flat/IVF), full HNSW parameters (connectivity, ef-construction/ef-search, levels, seed), and composition of engine-level options.

### Supporting types

`VectorMath`, `VectorScanner`, `VectorTopKBuffer` (SIMD kernels and top-k buffering); `VectorItemStore` (id → vector storage); `SeededRandom` (deterministic randomness).

---

## ProvostXMemory.VectorIndex.Gpu

ILGPU-backed GPU kernels with transparent CPU fallback.

### IGpuAccelerator (I) / GpuAccelerator (C)

Lazy, exception-guarded accelerator discovery and kernel launches; `IDisposable`.

### IGpuContextPolicy (I) / DefaultGpuContextPolicy (C)

Controls accelerator context creation (including a force-CPU test mode).

### IGpuScoreLauncher (I) / IlgpuScoreLauncher (C)

Launches similarity kernels over a `GpuVectorMatrix` batch; `GpuLaunchOptions` (chunk size, max parallel batches).

### GpuFlatIndex (C), GpuIvfIndex (C) : IVectorIndex

GPU versions of flat exact and IVF search. Delegate to an injected CPU `IVectorIndex` when no accelerator is available — bit-identical results by construction (no barriers/shared memory; one thread per slot, chunked launches).

### GpuVectorIndexFactory (C) : IVectorIndexFactory

Composes GPU indexes with an `EngineVectorIndexFactory` fallback.

### Supporting types

`GpuDiscoveryOptions`, `GpuLaunchOptions`, `GpuVectorKernels`, `GpuVectorMatrix`, `GpuMetricCodec`; per-backend `Options` records.

---

## ProvostXMemory.Lexical

### ILexicalIndex (I)

| Member | Description |
|---|---|
| `Count`, `Statistics` | Document count; live corpus statistics. |
| `Build(documents)` | Replace contents. |
| `IncrementalAdd(document)` | Add one document (duplicate ids throw). |
| `Remove(documentId)` | Remove by id; `false` when unknown. |
| `EnumerateDocuments()` | Deterministic id-ordered snapshot. |
| `Search(queryTokens, topK)` | BM25-scored top-k → `ScoredLexicalId`. |

**Usage note:** documents are pre-tokenized (`LexicalDocument`: `Id`, `BodyTerms`, `KeyTerms?`, `Category?`); tokenization itself is `LexicalTokenizer`.

### IBm25Scorer (I) / Bm25Scorer (C)

Saturated-TF IDF scoring (k1 = 1.2) with key-term boost (default 2.0) and a category-match bonus. Corpus statistics are passed per call.

### Supporting types

`LexicalPosting` (R) — (document id, term frequency); `LexicalTermCounts`, `TermStat` (R) — corpus statistics primitives.

---

## ProvostXMemory.Fusion

### IResultFuser (I) / ReciprocalRankFuser (C)

Fuses multiple ranked lists into one. Default implementation: Reciprocal Rank Fusion with `FusionOptions.RrfK` (default 60) and configurable dense/lexical weights; zero weights drop a stage; deterministic tie ordering. Fusion ids are scope-qualified so identical record ids across scopes never conflate.

---

## ProvostXMemory.Chunking

### IChunker (I)

`Split(record, options)` → `Chunk[]`. Implementations: `StructuredChunker` (structure-respecting), `SizeChunker` (size/overlap).

### IParentResolver (I) / ChunkParentResolver (C)

Chunk id → parent record lookup for parent-document recall.

### ChunkIdCodec (C), ChunkIdParts (R)

Encode/decode scope-qualified chunk ids (`scope kind, scope id, record id, chunk index`).

---

## ProvostXMemory.Reranking

### IReranker (I)

`Rerank(query, candidates)` → re-ranked candidates. Implementations:

| Type | Behavior |
|---|---|
| `NoopReranker` | Pass-through. |
| `ScoreBoostReranker` | Heuristic boosts (tag/key/category) + length penalties via `ScoreBoostRerankerOptions`. |
| `ClientBackedReranker` | Delegates to `IRerankerClient` with fallback fusion; `ClientBackedRerankerOptions.TrustClientCompletely`. |

### IRerankerClient (I)

Optional external rerank service contract (host-implemented; typically calls a cross-encoder or LLM endpoint).

---

## ProvostXMemory.Correction

### IRetrievalGrader (I)

`GradeAsync(query, candidate)` → `GradedCandidate` (`RetrievalGrade`: `Relevant | Irrelevant | Uncertain`). Host-injected.

### IQueryRewriter (I) / KeywordQueryRewriter (C)

Query rewriting for the corrective retry.

### IExternalFetcher (I)

Extension hook invoked when retrieval returns empty and `CorrectionOptions.UseFetcherOnEmpty` is set (e.g. web augmentation). The library never fetches on its own.

### CorrectiveRetriever (C) : ICorrectiveRetriever

Orchestrates grade → rewrite → retry loop; `CorrectionOptions` (`RelevanceFloor`, `UseFetcherOnEmpty`).

### Supporting types

`StageNames` (C) — stable stage-name constants for diagnostics.

---

## ProvostXMemory.Graph

### IMemoryGraphStore (I)

| Member | Description |
|---|---|
| `UpsertEntityAsync` / `UpsertRelationAsync` | Insert or update graph nodes/edges. |
| `UpsertClusterAsync(cluster)` | Replace-by-id cluster write (authoritative, not accumulating). |
| `GetEntityAsync(id)` | Fetch entity or `null`. |
| `SearchEntitiesAsync(query, max)` | Whole-token name/alias match, strongest overlap first. |
| `RelationsOfAsync(id, max)` | Undirected relations, strongest support first. |
| `NeighborsAsync(id, max)` | Direct neighbors. |
| `ShortestPathAsync(from, to)` | Shortest entity path. |
| `ClusterOfAsync(id)` | Cluster containing the entity, if any. |

Default implementation: `JsonlMemoryGraphStore` (scope-pinned JSONL files). `IMemoryGraphStoreResolver` supplies per-scope stores.

### IEntityExtractor (I) / HeuristicEntityExtractor (C)

Extracts entities + relations from records (`ExtractionResult`). Hosts may inject an LLM-backed extractor.

### IGraphRetriever (I) / GraphRetriever (C)

Retrieves records from the query-relevant subgraph plus cluster summaries; tuned by `GraphOptions`.

### Data records

`GraphEntity` (R) — id, name, aliases, record links; `GraphRelation` (R) — source, target, type, support; `GraphCluster` (R) — id, members, summary record id.

---

## ProvostXMemory.Consolidation

### IMemoryConsolidator (I)

`ConsolidateAsync(scopeKind, scopeId, options)` → surviving records. Default: `MemoryConsolidator`.

### IDedupDetector (I) / ThresholdDedupDetector (C)

Near-duplicate detection by embedding similarity threshold.

### IClusterSummarizer (I) / ClusterSummarizer (C)

Produces a summary record for a cluster; tuned by `ConsolidationTuning`.

### Supporting types

`ConsolidationReportMeta` (C) — pass report metadata.

---

## ProvostXMemory.Retrieval

### IHybridRetriever (I) / HybridRetriever (C)

Dense + lexical in parallel → fuse → resolve → optional rerank. Constructor takes the scope index registry, embedder, fuser, and optional telemetry, reranker, `FusionOptions`.

### IScopeIndexRegistry (I) / InMemoryScopeIndexRegistry (C)

Maps a scope to its artifacts: `GetVectorIndex(scope)`, `GetLexicalIndex(scope)`, `GetRecordProvider(scope)`, `HasIndexes(scope)`. Implementations must be **scope-exact** — no parent fallthrough, no default forwarding. The in-memory registry keys on the exact (Kind, Id) pair and also exposes `Register*` methods and a `Reset`.

### ICorrectiveRetriever (I)

Corrective-loop retrieval contract (implemented by `CorrectiveRetriever`).

### IGraphRetrieverImpl (I)

Graph retrieval implementation contract (implemented by `GraphRetriever`).

### IMultiRepresentationRetriever (I) / MultiRepresentationRetriever (C)

Query across modalities; resolves `repr|<recordId>|<index>` ids to best-representation scores per record.

### IQueryPlanRunner (I) / QueryPlanRunner (C)

Executes `QueryPlan`s: scope fan-out, merge, one weak-evidence rewrite retry (`IQueryTextRewriter`, default `KeywordQueryTextRewriter`; `QueryPlanRunnerOptions`).

### IScopeResolver (I) / RootChildScopeResolver (C)

Resolves plan target scope ids to concrete scopes (root + child interpretation).

### RetrievalFilterApplier (C)

Enforces `RetrievalFilters` on resolved candidates.

---

## ProvostXMemory.Eval

### EvalServiceFactory (I) / DefaultEvalServiceFactory (C)

`Create(rootPath)` builds a self-contained `IMemoryService` for evaluation; default factory composes the standard stack at a given vector dimension.

### EvalRunner (C)

| Member | Description |
|---|---|
| `LoadAsync(corpus, rootPath)` | Materializes the corpus into a fresh service and builds indexes. |
| `RunAsync(corpus, caseSet, options?)` | Full run on a fresh temp root: load → index → evaluate → report (root deleted unless `EvalRunnerOptions.DisposeRootDirectory = false`). |
| `EvaluateAsync(service, caseSet, options, totalRecords)` | Evaluates an already-loaded service. |

### EvalCase (C), EvalCaseSet (C)

Golden case: `Id`, `Query`, `QueryScope`, `RelevantIds`, optional graded relevance (0..1 per id; binary 1.0 when absent); `RelevanceOf(recordId)`. `EvalCaseSet` is a named, ordered case collection.

### EvalRunnerOptions (C), EvalMetricThresholds (C)

`TopK`, `Thresholds` (per-metric floors: `recallAtK`, `precisionAtK`, `mrr`, `ndcgAtK`), `DisposeRootDirectory`.

### Results

`EvalCaseMetrics` (R) — per-case metrics; `EvalMetricBreakdownEntry` (R) — per-scope/per-stage breakdowns; `EvalCaseTrace`/`EvalCaseTraceHit` (R) — hit traces with gains; `MetricThresholdViolation` (R) — metric, actual, threshold; `MetricThresholdViolations` (C) — violation collection; `EvalReportJson` (C) — canonical snake_case JSON codec.

### Deterministic corpus

`DeterministicEvalCorpus` (C) — seeded synthetic corpus (records, clusters, vector dimension); `ClusterHashEmbedder` (C) — deterministic embedder for reproducible runs; `EvalMetricMath` (C) — the metric implementations; `MemoryScopeComparer` (C) — deterministic scope ordering.

---

## ProvostXMemory.Logging

### MemoryLoggingOptions (R)

`Enabled`, `Level`, `Sink`, `Directory`, `FileNamePattern`, `Format`, `IncludeContent`, `Integrity`, `RetentionDays` — see the [options table](logging.md#options-reference).

### MemoryLogLevel (E), MemoryLogSinkKind (E), MemoryLogFormat (E), MemoryLogIntegrity (E)

Level (`Debug/Info/Warn/Error`); sink (`File/Console/Injected`); format (JSON Lines); integrity (hash-chain toggle).

### IMemoryLogSink (I)

`WriteAsync(entry)` — destination for `MemoryLogEntry`. Built-in: `FileMemoryLogSink` (rolling), `ConsoleMemoryLogSink`.

### MemoryLogger (C)

Central emitter; applies level filtering, format encoding, and chain appending. Disabled = zero writes/allocations.

### MemoryLogEntry (R), MemoryLogScope (R)

Entry payload: timestamp, sequence, level, category, scope, correlation id, duration, chain hashes, optional content excerpt.

### HashChainAppender (C), LogChainVerifier (C), LogChainVerificationResult (R)

SHA-256 chain append and verification (`IsValid`, broken-at sequence, detail).

### RetentionSweeper (C)

Deletes log files older than `RetentionDays`; explicit scheduling.

---

## ProvostXMemory.Telemetry

### IMemoryTelemetry (I)

Query timings, per-stage scores, backend stats — host-injected sink.

| Implementation | Behavior |
|---|---|
| `NoopMemoryTelemetry` | Silence (default; `Instance`). |
| `InMemoryMemoryTelemetry` | Collects events in memory for tests/diagnostics. |
