# ProvostXMemory — ARCHITECTURE

**License:** MIT. **Status:** complete, audited (2026-10-13). **Target:** .NET 8.

**Summary:** atomic, encapsulated .NET 8 memory/RAG library; hosts see only `IMemoryService` + typed options and inject their own embedder/grader/rewriter/telemetry; backends pluggable by config over an OWN C# vector engine (zero third-party vector dependencies); memory isolated per scope (platform/session/tenant/purpose/custom) by construction.

## Principles

- Strict OOP: interfaces first, constructor injection, no statics (pure-factory exceptions), one type per file
- Library knows NOTHING about its hosts; hosts know nothing about the internals
- No env vars in product code — config via typed options injected by the host
- All file I/O transactional (temp + atomic swap) at the storage layer
- Every public operation is scope-bound; cross-scope access is structurally impossible

## Layers (top → bottom)

```
L3 Application Facade   IMemoryService, MemoryServiceBuilder, query plans, eval runner
L2 Retrieval Features   hybrid search, fusion, reranking, corrective loop, graph retrieval,
                        multi-representation, query plans, scope registry
L1 Packages             stores, vector indexes (own engine), lexical index, chunker, graph store,
                        consolidation, correction, telemetry, eval primitives, logging
L0 Types/Atoms          records, results, options, contracts, primitive codecs
```

Dependency direction: strictly downward through interfaces. No cycles.

## L0 — Types (`ProvostXMemory.Abstractions`)

- `MemoryRecord` — Id, Scope, Key, Content, Category, Tags[], Reps[], CreatedAt, UpdatedAt, ExpiresAt?, Meta
- `MemoryScope` — Kind (Platform|Session|Tenant|Purpose|Custom), Id, ParentScope?; hierarchical, immutable
- `MemoryRepresentation` — Modality (Text|Image|Table|Audio|Custom), Content, EmbeddingModelHash?
- `ScoredMemoryRecord` — Record, Score, Provenance (retriever, stage scores)
- `RetrievalQuery` — Text, Scope, TopK, Filters (tags/category/date), Plan?
- `RetrievalResult` — IReadOnlyList<ScoredMemoryRecord>, Diagnostics (per-stage scores, timings)
- `Chunk`, `ChunkOptions` — chunk model + structural/size/overlap parameters
- `MemoryLoggingOptions` — logging/integrity/retention configuration (lives here so Abstractions stays dependency-free)
- `QueryPlan`, `QueryPlanStep`, `QueryTokens`, `RetrievalProvenance`, `RetrievalStageDiagnostics` — retrieval/pipeline atom types
- `MemoryManifest`, `MemoryEntryModel` — on-disk metadata codec contracts
- Options records: `MemoryServiceOptions`, `StorageOptions`, `VectorOptions` (+ `IndexKind`), `GraphOptions`, `FusionOptions`, `ConsolidationOptions`, `CorrectionOptions`

**Abstractions has ZERO project references** (verified against csproj) — dependency-free atom layer.

## L1 — Packages (each = its own csproj + tests)

**Storage** (`ProvostXMemory.Storage`)
- `IMemoryStore` — Add/Update/Delete/Get/List/Count(scope), Snapshot(scope)
- `IJsonlMemoryStore : IMemoryStore` — default store impl (`JsonlMemoryStore`: JSONL, transactional rewrites)
- `IMemoryManifestCodec` — manifest read/write (`MemoryManifestCodec`)
- `MemoryRootLayout` — root folder + scope path resolution (root/scopeKind/scopeId/modelHash)
- `AtomicFileWriter` — temp + atomic swap (POSIX-safe move, temp cleanup on failure)

**VectorIndex** (`ProvostXMemory.VectorIndex`)
- `IVectorIndex` — Build(items), Add, Remove, Search(vector, k) → ScoredIds; Dim; Count; Flush
- `IVectorIndexFactory` — creates Flat/Ivf/Hnsw indexes (own engine); `IndexKind` enum = Flat | Ivf | Hnsw | Gpu
- `IVectorIndexGuard` — per-index dim/epoch guard (rejects stale writes across rebuilds)
- `IVectorEmbedder` — Embed(text) → float[] (host-injected)
- `ScoredVectorId`, `VectorItem`, `VectorItemStore`, `VectorScanner`, `VectorTopKBuffer` — index primitives
- `VectorNamespaceFile` — per-scope/model-hash index file format + model-hash/dim guard
- `FlatJsonVectorIndex` — zero-dependency brute-force cosine backend, transactional JSON file
- `ScalarVectorOps`, `VectorMath` — SIMD math (Vector512 → Vector256 → Vector128 → scalar fallback; zero-vector guarded)
- `KMeansQuantizer`, `SeededRandom` — deterministic k-means + locked xorshift RNG

**VectorIndex.Engine** (`ProvostXMemory.VectorIndex.Engine`) — the OWN engine (no third-party vector deps):
- `FlatExactIndex` — SIMD exact brute-force scan
- `IvfFlatIndex` — k-means coarse quantizer + inverted lists (deterministic, empty-cluster reseating)
- `HnswIndex`/`HnswBuilder`/`HnswBeamSearcher` — **immutable-index + batch-rebuild semantics** (see Patent notes)

**VectorIndex.Gpu** (`ProvostXMemory.VectorIndex.Gpu`, optional)
- ILGPU 1.5.3 kernels (Flat + IVF): `GpuFlatIndex`, `GpuIvfIndex`, one-thread-per-slot, chunked launches, no barriers/shared memory → **bit-identical accumulation order CPU/GPU**
- `IGpuAccelerator` availability gate — lazy, exception-guarded; transparent injected-CPU fallback; no Engine-package reference

**Lexical** (`ProvostXMemory.Lexical`)
- `ILexicalIndex` — Build/IncrementalAdd/Remove/EnumerateDocuments/Search(terms, k) → ScoredIds
- `IBm25Scorer` — saturated-TF IDF scoring + key boost

**Chunking** (`ProvostXMemory.Chunking`)
- `IChunker` — Split(record, options) → Chunk[] (`SizeChunker`, `StructuredChunker`)
- `IParentResolver` — chunk id → parent record (parent-document recall)
- `ChunkIdCodec` — chunk id codec; **rejects `|` in scope/record ids loudly** (silent dense-recall loss otherwise)

**Fusion** (`ProvostXMemory.Fusion`)
- `IResultFuser` — Fuse(ranked lists, weights) → fused ranked list (`ReciprocalRankFuser`, RRF k=60, weighted, deterministic tie order)

**Reranking** (`ProvostXMemory.Reranking`)
- `IReranker` — Rerank(query, candidates) → reranked (`ScoreBoostReranker` additive, no drops; `NoopReranker`)
- `IRerankerClient` — external rerank service contract (`ClientBackedReranker`)

**Graph** (`ProvostXMemory.Graph`)
- `IMemoryGraphStore` — UpsertEntity/UpsertRelation/Neighbors/ShortestPath/ClusterOf (`JsonlMemoryGraphStore`, scope-pinned)
- `IEntityExtractor` — Extract(records) → entities+relations (host-injected; `HeuristicEntityExtractor` default)
- `IGraphRetriever` — Retrieve(query, scope) → records + cluster summaries (`GraphRetriever`, deterministic BFS)
- `GraphIndexLedger` — facade-side graph extraction ledger

**Correction** (`ProvostXMemory.Correction`)
- `IRetrievalGrader` — Grade(query, candidate) → Relevant/Irrelevant/Uncertain (host-injected)
- `IQueryRewriter` — Rewrite(query) → expanded/keyword query (`KeywordQueryRewriter` default)
- `CorrectiveRetriever` — grade→external-fetch-hook→rewrite loop via injected `IExternalFetcher` (bounded retries)

**Consolidation** (`ProvostXMemory.Consolidation`)
- `IMemoryConsolidator` — pass over scope: dedup, TTL expiry, cluster summary emission (`MemoryConsolidator`)
- `IDedupDetector` (`ThresholdDedupDetector`) — near-duplicate detection (oldest kept, tags unioned)
- `IClusterSummarizer` — summarize cluster into a summary record

**Telemetry** (`ProvostXMemory.Telemetry`)
- `IMemoryTelemetry` — query timings, per-stage scores, backend stats (host sink; `NoopMemoryTelemetry`/`InMemoryMemoryTelemetry`)

**Eval** (`ProvostXMemory.Eval`) — deterministic evaluation primitives
- `EvalCase`/`EvalCaseSet`/`DeterministicEvalCorpus` — reproducible golden corpora
- `ClusterHashEmbedder` — deterministic hash embedder (no real model anywhere)
- `EvalMetrics`/`EvalMetricMath` — Recall@k, Precision@k, MRR, NDCG@k + per-scope/per-stage breakdowns
- `EvalReport`/`EvalReportJson`/`EvalMetricThresholds` — serializable results + threshold gates
- Eval references only Abstractions + VectorIndex (downward only)

**Logging** (`ProvostXMemory.Logging`) — leaf package (depends only on Abstractions)
- `IMemoryLogSink` + `ConsoleMemoryLogSink`/`FileMemoryLogSink` — rolling files, JSON lines or text
- `MemoryLogger` — facade-facing logger (toggle/level via `MemoryLoggingOptions`)
- `HashChainAppender`/`LogChainVerifier` — SHA-256 hash-chain integrity (covers timestamp+seq+content+prev-hash; **serialized under a gate** — concurrent writes never interleave chain links)
- `RetentionSweeper` — deletes log files past the retention window (pattern-scoped deletes)

## L2 — Retrieval Features (`ProvostXMemory.Retrieval`)

- `IHybridRetriever` → `HybridRetriever` — dense + lexical in parallel → `IResultFuser` → resolve → optional `IReranker` (exactly ONE rerank pass; per-stage diagnostics)
- `ICorrectiveRetriever` — grader-gated hybrid retrieval with rewrite loop (Correction package)
- `IMultiRepresentationRetriever` → `MultiRepresentationRetriever` — query across modalities (`repr|<recordId>|<index>` ids; representation ids reject the separator loudly)
- `IQueryPlanRunner` → `QueryPlanRunner` — executes declarative `QueryPlan`: target-scope fan-out, scope-qualified fusion ids (same record id in two scopes never conflates), weak-evidence single rewrite-retry
- `IScopeIndexRegistry` → `InMemoryScopeIndexRegistry` — scope-exact artifact registry (vector/lexical/record provider per scope; exact-scope keys)

## L3 — Application Facade (`ProvostXMemory` root package)

- `IMemoryService` — THE public surface:
  - `RememberAsync`/`ForgetAsync`/`UpdateAsync` — store + dense/lexical index maintenance via per-record id ledger; optional graph extraction
  - `RecallAsync(RetrievalQuery) → RetrievalResult` — routes: plan → corrective (when grader wired) → hybrid → optional rerank (single rerank pass)
  - `ConsolidateAsync(scope, options)` — delegates to `IMemoryConsolidator`, refreshes/removes index artifacts
  - `BuildIndexesAsync(scope)`/`RebuildAsync` — chunk + embed + vector + lexical build per scope
  - `Scopes()` — enumerate existing scopes (`IScopesAware` store hook, directory-layout fallback)
- `MemoryService` — composition root; takes `MemoryServiceOptions` + injected deps
- `MemoryServiceBuilder` — fluent wiring helper (still DI, no service locator)
- `EvalRunner` + `DefaultEvalServiceFactory` live HERE (they compose a full `IMemoryService`; keeps Eval a pure L1 — no L1→L3 edge)
- `RootChildScopeResolver`, `MemoryServicePipelineOptions`, `RetrievalFilterApplier`

## Backend matrix

| Backend | Package | Notes |
|---|---|---|
| FlatJson (default) | core | zero-dependency brute-force cosine, transactional JSON file |
| FlatExact (SIMD) | `…VectorIndex.Engine` | own engine, Vector512 kernels, exact search |
| IvfFlat | `…VectorIndex.Engine` | own engine, deterministic k-means coarse quantizer + inverted lists |
| Hnsw | `…VectorIndex.Engine` | own engine, **immutable-index + batch-rebuild** semantics |
| Gpu (ILGPU, optional) | `…VectorIndex.Gpu` | own kernels (Flat + IVF), lazy guarded availability, transparent CPU fallback (bit-identical results) |
| Native FAISS (P/Invoke) | **not adopted** | own engine replaces it; zero third-party vector deps |

All backends pass the SAME `IVectorIndex` conformance suite; cross-backend parity and determinism pinned by tests.

## Concurrency & correctness model

- **Per-service async store gate** (`SemaphoreSlim`): store write + index mutation + graph update serialize across Remember/Forget/Update; embeddings compute OUTSIDE the gate (parallel-safe)
- **Per-service lexical mutation gate**: lexical index (plain Dictionary) mutations never race readers
- **Serialized hash-chain logging gate**: concurrent log entries never interleave chain links
- Index guard (`IVectorIndexGuard`): dim/epoch mismatched (stale, post-rebuild) writes rejected loudly
- Everything else audited clean (three full-context audits 2026-10-13): no races beyond the gates above, no leaks (scope isolation structurally enforced), no unhandled IO paths, all traversals hop-bounded/visited-guarded

## Dependency flow

```
MemoryService (L3) → IHybridRetriever/ICorrectiveRetriever/IQueryPlanRunner (L2)
                       → IFusion/ILexical/IVectorIndex/IMemoryGraphStore/IRetrievalGrader (L1)
                          → IMemoryStore + record types (L0)
```

- Abstractions (L0): zero references
- Eval (L1): Abstractions + VectorIndex only, downward
- VectorIndex.Gpu: VectorIndex interfaces only (CPU fallback index injected) — no Engine reference
- Logging (L1): leaf, Abstractions only
- Facade (L3): references Eval (runner/factory composition) + all backend packages
- Hosts reference ONLY the root package + optionally backend packages

## Storage layout (per root folder, host-configured)

```
<root>/
  manifest.json
  <scope-kind>/<scope-id>/
    records.jsonl
    chunks/
    vectors/<model-hash>/index.json        (FlatJson backend)
    graph/entities.jsonl, relations.jsonl, clusters.jsonl
```

## Testing strategy

- One test project per package (17 suites) + `ProvostXMemory.SystemTests` — **host-agnostic**: zero references to any product; exercises ONLY the public surface with `ClusterHashEmbedder` + temp-dir store
- Tiers: UNIT (scope/filters/chunker/fuser/HNSW-determinism/report round-trip), INTEGRATION (backend matrix parity: FlatExact/Ivf/Hnsw/FlatJson/Gpu-fallback — identical deterministic rankings cross-backend; persistence/restart; consolidation; graph clusters), JOURNEY (knowledge assistant Recall@5 ≥ 0.9 — achieved 1.0; multi-tenant 3-scope isolation, zero leakage incl. restart; lifecycle)
- 5 real concurrency/durability bugs found by audits and pinned by regression tests: hash-chain gate, `ChunkIdCodec` pipe rejection, facade double-rerank removal, lexical mutation gate, store gate
- **Suite total: 626 green, 0 warnings / 0 errors on .NET 8**

## Known semantics (documented decisions, not bugs)

- `QueryPlanRunnerOptions.WeakEvidenceThreshold` (default 0.02) is on the RRF scale: with a rewriter wired, pass-2 (rewritten) fully REPLACES pass-1 when the top hit lacks two-stage corroboration — retry-only-when-uncorroborated semantics
- `JsonlMemoryStore.WriteEntryAsync` appends replaced entries at file end while `UpdateAsync` preserves position — both full rewrites; consumers re-sort
- Cold-index semantics: recall on a fresh `MemoryService` over an existing root is empty until `BuildIndexesAsync`/`RebuildAsync`

## Patent & license notes (own engine)

- Owner decision (2026-10-13): build our own FAISS-equivalent in C#; ALL third-party vector-index packages dropped (Gravicode.FaissNet unverified license; FAISS ships no .NET bindings)
- Patent review: NO broad granted patent on HNSW core (build + greedy layered search). Sole known claim = US application 15/929,802 "Dynamic Updates for HNSW" (narrow: incremental updates on live graphs, pending). Our HNSW uses **immutable built indexes + batch rebuild on mutation** — it never implements the claimed dynamic-update method. IVF/Flat = classic methods, no blocking claims. Earlier "Yandex HNSW patent" note was wrong (unrelated number) — corrected.
- ILGPU 1.5.3: NCSA license (MIT-compatible), verified against the license file shipped in the nupkg — see THIRD-PARTY-NOTICES.md
- Periodic HNSW patent-status re-check: owner-gated backlog item (see docs/BACKLOG-MASTER.md)

## Documentation artifacts

- README.md — landing (features, quickstart, backend matrix, design principles)
- docs/ — getting-started, concepts, scopes, retrieval, backends, consolidation, evaluation, logging, api-glossary, FAQ
- docs/api-site/ — DocFX API reference (265 pages), generated from the shared corporate template (`~/Agent/ProvostX/docs/templates/docfx-template/`)
- docs/wiki.html — single-file offline wiki browser (generated by `.ldc/gen_wiki.py`)
- API-INDEX.md / RELATIONSHIP-GRAPH.md / PACKAGE-MAP.md — LDC artifacts (generated by `.ldc/gen_ldc.py`)
- docs/BACKLOG-MASTER.md — owner-gated backlog (NuGet publish, Inference-backed embedder adapter, HNSW patent re-check)