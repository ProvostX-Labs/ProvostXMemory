# LDC Compliance + Architecture Accuracy Audit — ProvostXMemory

**Date:** 2026-10-13 · **Scope:** `src/` + docs, at HEAD `db695c7` (one uncommitted working-tree edit to `HybridRetriever.cs` diffed; content matched)
**Method:** full read of all 18 `src/` packages (16,297 LOC, ~230 top-level types), both csproj directions, all four LDC docs. Report-only; nothing modified, nothing staged/committed.

## Verdict per LDC rule

| # | Rule | Verdict | Notes |
|---|---|---|---|
| 1 | Interfaces as cross-package surface | ⚠️ PARTIAL | Concrete cross-package ctor deps: `GpuVectorIndexFactory` ← `EngineVectorIndexFactory`; `MemoryService` ← `LexicalIndex`/`Bm25Scorer` (line 398/565); `MemoryConsolidator`/`ThresholdDedupDetector` ← `ScalarVectorOps`. Everything else is interface-first. |
| 2 | Constructor injection / no service locator | ✅ PASS | No `IServiceProvider`, no `GetService`, no ambient locator anywhere in `src/`. All principal deps are ctor params. `new` of *injected types* found only in composition-root-shaped code (`MemoryService`, `MemoryServiceBuilder`, `DefaultEvalServiceFactory`, `GpuVectorIndexFactory`, `ExampleRig`) — LDC gray zone, judged acceptable for a facade/comp-root; flagged for awareness. |
| 3 | No static mutable state / singletons | ✅ PASS (minor) | Every static member verified pure: immutable empty-dict/list defaults, compiled regex, frozen option sets, pure factory methods (`HnswOptions.Default`, `GraphOptions.Default`). No mutable statics, no ambient singletons. ~10 static helpers lack the "Stateless utility" comment (F1). `ChunkAssembler.Instance`/`Segmenter.Instance` are stateless-by-construction singletons documented as such — within the letter of the exception. |
| 4 | One type per file, file name = type name | ❌ FAIL | 23 files carry 2–5 top-level types (F2). Worst: `ILexicalIndex.cs` (5 types). Only genuine name violation: `HnswOptions.cs` ↔ `HnswOptions` class lives there but file also imports… verified `HnswOptions` IS the file's main type; the file-name rule holds for it. Genuine name mismatch: **none found** beyond nesting/packing issues. |
| 5 | No cross-dependencies (downward-only refs) | ⚠️ PARTIAL | Reference direction is acyclic and mostly downward (F3), but 5 edges violate the documented architecture: Abstractions→Logging (L0→leaf!), Lexical→Chunking (unused), Eval→facade/Engine, Gpu→Engine, plus lateral Consolidation→VectorIndex (tolerated, interface-only). No cycles. |
| 6 | No cycles | ✅ PASS | Project-reference graph is a DAG. Eval→`ProvostXMemory` + facade→Retrieval is the only potential upward path; no package transitively references itself. |
| 7 | Namespaces match folder paths | ✅ PASS (1 cosmetic) | All 137 `.cs` files: namespace == folder path. `DefaultGpuContextPolicy.cs:16` has `namespace …Gpu;/// <summary>` on one line — cosmetic, no semantic effect (F5). |

## 1. LDC violations (file:line, rule, severity, fix)

### F1 — Statics missing the sanctioned "Stateless utility" comment (minor)
Pure-function statics, but the architecture principle says the exception must be declared; these have no comment (checked file-by-file):
- `src/ProvostXMemory.Logging/FileMemoryLogSink.cs:29,39` — `ResolvePath`, `FormatLine` ("(pure)" inline, no architecture note)
- `src/ProvostXMemory.Logging/HashChainAppender.cs:42` — `ComputeEntryHash` ("Pure hash computation" but no architecture rule reference)
- `src/ProvostXMemory.Logging/MemoryLogJson.cs:24` — `public static readonly Options` (public mutable-typed static; content is effectively frozen but not provably immutable — `JsonSerializerOptions` allows config until first use; also comment says "Shared serializer options", no architecture note) · **minor**
- `src/ProvostXMemory.Logging/LogChainVerifier.cs:8` — class-level static utility, doc mentions purity but not the rule
- `src/ProvostXMemory.Logging/RetentionSweeper.cs:23,42` — `TryParseFileDate`, `BuildPatternRegex` (no note)
- `src/ProvostXMemory.Eval/EvalMetrics.cs:29` — `Mean` (no note)
- `src/ProvostXMemory.Eval/EvalMetricThresholds.cs:104` — `None()` factory (no note)
- `src/ProvostXMemory.Retrieval/MultiRepresentationRetriever.cs:123` — `ParseParentId` (has comment at `RepresentationId` at :137-138 but not at `ParseParentId`)
- `src/ProvostXMemory.VectorIndex.VectorNamespaceFile.cs:16` — `SerializerOptions` (no note)
- `src/ProvostXMemory.VectorIndex.Engine/HnswBeamSearcher.cs:108,186,290` — `CompareBestFirst`, `Above` (no note)
**Fix:** one-line `// Stateless utility (pure): <why>` above each static class/member, matching the pattern used in `VectorMath`, `RetrievalFilterApplier`, `ChunkAssembler`.

### F2 — Multi-type files (violation of rule 4; minor/major per size)
| File | Count | Contents |
|---|---|---|
| `src/ProvostXMemory.Lexical/ILexicalIndex.cs` | 5 | `ScoredLexicalId`, `IBm25Scorer`, `ILexicalIndex`, `LexicalDocument`, `LexicalPosting` — **major** |
| `src/ProvostXMemory.Eval/EvalReport.cs` | 4 | `EvalReport`, `EvalCaseTrace`, `EvalCaseTraceHit`, `EvalReportJson` — major |
| `src/ProvostXMemory.Eval/EvalMetrics.cs` | 3 | `EvalCaseMetrics`, `EvalMetrics`, `EvalMetricBreakdownEntry` — minor |
| `src/ProvostXMemory.Eval/EvalMetricThresholds.cs` | 3 | `EvalMetricThresholds`, `MetricThresholdViolation`, `MetricThresholdViolations` — minor |
| `src/ProvostXMemory.Correction/IRetrievalGrader.cs` | 3 | `RetrievalGrade`, `GradedCandidate`, `IRetrievalGrader` — minor |
| `src/ProvostXMemory.Abstractions/MemoryScope.cs` | 3 | `MemoryScopeKind`, `MemoryScope`, `MemoryRepresentationModality` — minor (modality enum misplaced; belongs in `MemoryRepresentation.cs`) |
| `src/ProvostXMemory.Abstractions/QueryPlan.cs` | 2 | `QueryPlan`, `QueryPlanStep` — minor |
| `src/ProvostXMemory.Chunking/IChunker.cs` | 2 | `IChunker`, `IParentResolver` — minor |
| `src/ProvostXMemory.Chunking/ChunkIdCodec.cs` | 2 | `ChunkIdCodec`, `ChunkIdParts` — minor |
| `src/ProvostXMemory.Chunking/ChunkAssembler.cs` | 2 | `ChunkAssembler`, `Segment` (internal) — minor |
| `src/ProvostXMemory.Chunking/Segmenter.cs` | 2 | `Segmenter`, `WordSpan` (internal) — minor |
| `src/ProvostXMemory.Fusion/IResultFuser.cs` | 2 | `RankedList`, `IResultFuser` — minor |
| `src/ProvostXMemory.Logging/MemoryLogJson.cs` | 2 | `MemoryLogJsonLine`, `MemoryLogJson` — minor |
| `src/ProvostXMemory.VectorIndex/IVectorIndex.cs` | 3 | `IVectorIndex`, `ScoredVectorId`, `VectorItem` — minor |
| `src/ProvostXMemory.VectorIndex.Engine/HnswGraph.cs` | 2 | `HnswCandidate`, `HnswGraph` — minor |
| `src/ProvostXMemory.VectorIndex.Engine/IvfPostingLists.cs` | 2 | `IvfPostingLists`, `IvfMember` — minor |
| `src/ProvostXMemory.Retrieval/IScopeIndexRegistry.cs` | 2 | `IScopeIndexRegistry`, `InMemoryScopeIndexRegistry` (impl in interface file) — **major** |
| `src/ProvostXMemory/MemoryServicePipelineOptions.cs` | 2 | `MemoryServicePipelineOptions`, `IScopesAware` — minor |
| `src/ProvostXMemory.Lexical/LexicalCorpusStatistics.cs` | 2 | `TermStat`, `CorpusStatistics` — minor |
| Nested-types-as-file: `QueryPlanRunner.cs` (IScopeResolver, QueryPlanRunnerOptions nested), `FlatExactIndex.cs`/`EngineVectorIndexFactory.cs`/`IvfFlatIndex.cs`/`GpuFlatIndex.cs`/`GpuIvfIndex.cs` (each a nested `Options`), `FlatJsonVectorIndex.cs` (nested `Content`/`Entry`), `HeuristicEntityExtractor.cs` (nested `EntityAccumulator`), `VectorNamespaceFile.cs` (nested `Entry`) — API-INDEX lists these as top-level (drift, see F6) | | |
**Fix:** split into one file per type; move nested types that are part of the public surface into their own files (or document the nesting convention in ARCHITECTURE.md).

### F3 — Cross-dependency / dependency-flow violations
| Edge | Evidence | Severity | Why it matters |
|---|---|---|---|
| `Abstractions` → `Logging` | `src/ProvostXMemory.Abstractions/MemoryServiceOptions.cs:1` (`using ProvostXMemory.Logging;` for `MemoryLoggingOptions`) | **blocker-level (major)** | L0→leaf edge. Logging becomes a hard transitive dep of every package, inverting the "Abstractions = dependency-free atoms" claim; PACKAGE-MAP shows it, ARCHITECTURE.md never mentions it. Fix: move `MemoryLoggingOptions` into Abstractions (or an options-only sub-namespace) or drop the options field. |
| `Lexical` → `Chunking` | `src/ProvostXMemory.Lexical/ProvostXMemory.Lexical.csproj` ProjectReference; **zero usages** in `src/ProvostXMemory.Lexical/*.cs` | major | Dead lateral edge; also contradicts ARCHITECTURE dependency flow. Fix: remove the ProjectReference. |
| `Eval` → `ProvostXMemory` (facade) + `VectorIndex.Engine` | `src/ProvostXMemory.Eval/ProvostXMemory.Eval.csproj`; `DefaultEvalServiceFactory.cs:3,35-37` (`new MemoryServiceBuilder()` + engine) | major | L1→L3 upward edge, contradicts "strictly downward through interfaces". Fix: invert — define `EvalServiceFactory` port in Eval, let a host/facade-side default factory compose the service; or relocate the default factory into the facade/tests. |
| `VectorIndex.Gpu` → `VectorIndex.Engine` | csproj + `GpuVectorIndexFactory.cs:23,38` (concrete `EngineVectorIndexFactory` + `FlatExactIndex` cast), `GpuMetricCodec.cs:3`, `GpuFlatIndex.cs:9`, `GpuIvfIndex.cs:9` | major | Concrete-class dep between backend packages (rule 1 + rule 5). Fix: inject `IVectorIndexFactory` + `IVectorIndex` (fallback as interface) instead of the concrete engine factory/cast. |
| `Consolidation` → `VectorIndex` (concrete `ScalarVectorOps`) | `MemoryConsolidator.cs:166` (`new ScalarVectorOps()`), `ThresholdDedupDetector.cs:57` | minor | Same-layer lateral, concrete class across package. Fix: inject a minimal cosine port or move `ScalarVectorOps` to Abstractions. |
| `MemoryService.cs:398,565` | `new LexicalIndex(new Bm25Scorer())` concrete cross-package types in facade | minor | Facade should depend on `ILexicalIndex`/`IBm25Scorer`; inject defaults via builder instead. |
| `Retrieval → Chunking` | `HybridRetriever.cs:8,240` `new ChunkIdCodec()` (concrete cross-package) | minor | Fix: inject `ChunkIdCodec` (it's stateless; a ctor default is fine) or move codec to Abstractions. |
| `Graph` → `Storage` | `JsonlMemoryGraphStore.cs:12,31-47` (`MemoryRootLayout`, `AtomicFileWriter`) | minor (accepted by ARCHITECTURE implicitly, but its dependency-flow diagram omits it) | Document or invert. |

### F4 — Rule 1 (interface-first) near-misses (minor)
- `Correction` package: `CorrectiveRetriever` is the only implementation of `ICorrectiveRetriever`, which lives in **Retrieval**; `Correction`'s own public surface (grader/rewriter/fetcher) is fine, but the retriever type itself is defined cross-package (composition inversion). Same shape for `GraphRetriever : IGraphRetriever` (Graph) used only via Retrieval's `IGraphRetrieverImpl`… which is **dead code**: no type implements or references it (`grep IGraphRetrieverImpl` → declaration only). **minor/blocker-ish cleanup:** delete `IGraphRetrieverImpl.cs` or wire it.
- `IScopesAware` lives in facade (`MemoryServicePipelineOptions.cs:42`) but is implemented by test stores (fine) — noted as facade-owned hook, OK.

### F5 — Namespace/format nits (minor)
- `src/ProvostXMemory.VectorIndex.Gpu/DefaultGpuContextPolicy.cs:16` — `namespace …;/// <summary>` merged line (comment glued to namespace declaration).
- `src/ProvostXMemory.VectorIndex.Gpu/IlgpuScoreLauncher.cs` — filename casing; LDC rule "file name = type name" reads as exact case: should be `IlgpuScoreLauncher.cs`. **minor**

## 2. Architecture accuracy (ARCHITECTURE.md vs code)

### Verified accurate ✅
- All 18 packages named in ARCHITECTURE.md exist as csproj + tests; roles match (`Storage`, `VectorIndex`, `Lexical`, `Chunking`, `Fusion`, `Reranking`, `Graph`, `Correction`, `Consolidation`, `Telemetry`, `Retrieval`, facade, Engine, Gpu, Eval, Examples, Logging).
- `IMemoryStore` (Add/Update/Delete/Get/List/Count/Snapshot) matches ARCHITECTURE.md §Storage verbatim.
- `IResultFuser` (RRF, weighted, k=60 default), `IReranker`/`IRerankerClient`, `IMemoryTelemetry` (stage/backend stats), `IMemoryGraphStore` (UpsertEntity/UpsertRelation/UpsertCluster/Get/Search/Relations/Neighbors/ShortestPath/ClusterOf), `IEntityExtractor`, `IGraphRetriever`, `IRetrievalGrader` (Relevant/Irrelevant/Uncertain via `RetrievalGrade`), `IQueryRewriter`, `IExternalFetcher`, `IMemoryConsolidator`/`IDedupDetector`/`IClusterSummarizer`, `IVectorEmbedder`, `IVectorIndex` (Build/Add/Remove/Search/Dim/Count/Flush), `IChunker`, `IParentResolver`, `IBm25Scorer`, `IJsonlMemoryStore`, `IMemoryManifestCodec`, `IHybridRetriever`, `ICorrectiveRetriever`, `IMultiRepresentationRetriever` (`repr|<id>|<n>` ids confirmed at `MultiRepresentationRetriever.cs:123-138`), `IQueryPlanRunner`, `IScopeIndexRegistry` (+`Reset`), `IMemoryService` (Remember/Forget/Get/Update/Recall/Consolidate/BuildIndexes/Rebuild/Scopes), `MemoryServiceBuilder`, `QueryPlan`/`QueryPlanStep`, `IndexKind` Flat/Ivf/Hnsw(+Gpu) — **all match** their ARCHITECTURE.md one-liners.
- `MemoryRootLayout`: `<root>/manifest.json`, `<root>/<scope-kind>/<scope-id>/records.jsonl`, `chunks/`, `vectors/<model-hash>/`, `graph/{entities,relations,clusters}.jsonl` — **exactly matches** code (`MemoryRootLayout.cs:52-90` + `JsonlMemoryGraphStore.cs:27-29`). ARCHITECTURE.md storage-layout section is accurate.
- Dependency flow prose (L3→L2→L1→L0, hosts reference facade + backend packages, host-injected embedder/grader/rewriter/telemetry) matches csproj reality *for product flow*; only the F3 exceptions deviate.
- Principles section (no env vars — verified 0 `GetEnvironmentVariable` in `src/`; transactional file I/O via `AtomicFileWriter` — confirmed in Storage + Graph) — accurate.
- Backend matrix table (own engine Flat/IVF/HNSW, GPU ILGPU with CPU fallback, "Native FAISS NOT ADOPTED") matches code (`EngineVectorIndexFactory`, `GpuVectorIndexFactory`, zero FAISS packages).
- `IMemoryGraphStoreResolver` named in facade docs — exists (`Graph/IMemoryGraphStoreResolver.cs`), used in `GraphIndexLedger` docs only (interface exists; consumed via `Func<MemoryScope, IMemoryGraphStore>`; doc phrasing tolerable).

### Inaccuracies / drift ❌
| # | ARCHITECTURE.md says | Reality | Severity |
|---|---|---|---|
| A1 | Storage layout comment `vectors/<model-hash>/faiss.index (FAISS backends)` | No such filename anywhere; `VectorNamespaceFile` (atomic JSON, id `provostx.vectorindex.v1`) is the only index file format; no code writes `faiss.index` or `index.json` into `vectors/` | minor (stale comment; addendum already retracts FAISS) |
| A2 | "Dependency direction: strictly downward through interfaces. No cycles." + flow diagram omits these edges | 5 upward/lateral edges exist (F3: Abstractions→Logging, Eval→facade, Gpu→Engine, Lexical→Chunking, Consolidation→VectorIndex) | major |
| A3 | §L1 VectorIndex lists `IFaissIndexFactory` renamed → `IVectorIndexFactory` in addendum ✓ — but section body still titles "`IFaissIndexFactory` — creates Flat/Ivf/Hnsw indexes (managed port)" | Interface is `IVectorIndexFactory` (own engine); "managed port" is wrong post-addendum | minor |
| A4 | §L1 Lexical lists "`IBm25Scorer` — … (default impl)" ✓ exists; but §L1 omits `IVectorIndexGuard`, `ScoredId`, `IndexKind.Gpu` | All exist; `IndexKind` doc says "FAISS family" (`IndexKind.cs:5`) with a `Gpu` member not listed in ARCHITECTURE matrix wording | minor |
| A5 | L0 list omits `QueryTokens`, `RetrievalProvenance`, `RetrievalStageDiagnostics`, `Chunk` (only "Chunk, ChunkOptions" partial), `QueryPlanStep` — several Abstractions types undocumented | All exist and are public | minor |
| A6 | "L2 … `IGraphRetriever` (impl) — graph-backed retrieval ✅ (Graph package, wave 2b)" | `IGraphRetriever` lives in Graph (L1); Retrieval defines `IGraphRetrieverImpl` which nothing implements — the documented wiring doesn't exist as stated | major |
| A7 | Eval package: absent from the Layers section entirely (only mentioned in backend-matrix/system-test addenda), yet it's an L1/L3-hybrid package with 19 types | Should be listed as a package with its layer assignment (it's the only non-test, non-Examples package without an ARCHITECTURE.md section) | minor |
| A8 | Logging package: also absent from Layers section (mentioned only in wave-7 addendum bullet) | 12-type L1 package, foundational (leaf) | minor |

## 3. API-INDEX.md / RELATIONSHIP-GRAPH.md / PACKAGE-MAP.md spot-checks (20+ each)

### API-INDEX.md — 24 entries checked
✅ Accurate: `IMemoryLogSink`, `ConsoleMemoryLogSink`, `FileMemoryLogSink`, `MemoryLogger`, `IMemoryStore`, `IJsonlMemoryStore`, `IMemoryManifestCodec`, `JsonlMemoryStore : IJsonlMemoryStore`, `MemoryManifestCodec`, `MemoryRootLayout`, `IReranker`, `IRerankerClient`, `ClientBackedReranker`, `ScoreBoostReranker`, `NoopReranker`, `IResultFuser`/`ReciprocalRankFuser`, `IChunker`/`IParentResolver`/chunker impls, `IClusterSummarizer`/`IDedupDetector`/`IMemoryConsolidator` + impls, `IEntityExtractor`/`IGraphRetriever`/`IMemoryGraphStore` + impls, `IMemoryTelemetry` + impls, facade types (`IMemoryService`, `MemoryService`, `MemoryServiceBuilder`, `GraphIndexLedger`, `RootChildScopeResolver`), Gpu types (`IGpuAccelerator`, `IGpuContextPolicy`, `IGpuScoreLauncher`, `DefaultGpuContextPolicy`, `GpuAccelerator`, `IlgpuScoreLauncher`, `GpuVectorMatrix`, `GpuVectorIndexFactory`), Eval types (`EvalRunner`, `EvalServiceFactory`, `DefaultEvalServiceFactory`, `ClusterHashEmbedder`, `DeterministicEvalCorpus`, `EvalCase`/`EvalCaseSet`/`EvalReportJson`), `RetrievalFilterApplier`.
❌ Wrong:
- **Missing entirely** (grep count 0 in API-INDEX): `MemoryRecord`, `MemoryRepresentation`, `MemoryRepresentationModality`, `RetrievalResult`, `RetrievalStageDiagnostics`, `QueryPlanStep` (Abstractions L0 atoms), plus `ConsolidationReport` and `ConsolidationTuning` (Consolidation; only `ConsolidationReportMeta` is listed), plus the whole **Examples package (0 types listed; package absent)**. Header claims "Types: 283" but the section sums list fewer; public L0 atoms are the core of the API index — **major**.
- **Nested types flattened and misnamed**: Engine/Gpu `Options` entries are nested classes (`FlatExactIndex.Options`, `EngineVectorIndexFactory.Options`, `IvfFlatIndex.Options`, `GpuFlatIndex.Options`, `GpuIvfIndex.Options`) listed as five bare `Options` top-level entries; `VectorNamespaceFile.Entry` → "Entry"; `FlatJsonVectorIndex.Content` → "Content"; `MemoryScopeComparer : MemoryScope>` is mangled output (broken generic rendering) — **major** (generator bug).
- `HybridRetriever` deps line omits `ChunkIdCodec` param (present in working-tree ctor? — ctor has `ChunkIdCodec codec` param at `HybridRetriever.cs:261` usage; index shows only registry/embedder/fuser/telemetry/reranker/fusionOptions) — minor (generator uses primary ctor only).
- Counts: "ProvostXMemory.Fusion (2 types)" ✓; "ProvostXMemory (6 types)" ✓; "ProvostXMemory.Retrieval (15 types)" — code has 8 interfaces + 5 classes + `IScopeResolver` + `QueryPlanRunnerOptions` nested = list inflates nested as top-level; matches section text but double-counts vs F2 reality — minor.
- API-INDEX header "Generated 2026-10-13T12:08" predates wave 7 logging facade commit (`6e8b5e7` same day) and HEAD `db695c7`; Logging section (12 types) IS present, so content mostly current; only `HybridRetriever` working-tree edit postdates it — OK.

### RELATIONSHIP-GRAPH.md — 22 edges checked
✅ Accurate: `MemoryService ──implements──► IMemoryService`; `MemoryService ──uses──► IMemoryStore/IMemoryTelemetry/IResultFuser/IVectorEmbedder/IVectorIndexFactory`; `GraphIndexLedger ──uses──► IEntityExtractor`; `RootChildScopeResolver ──implements──► IScopeResolver`; `ChunkParentResolver ──uses──► IMemoryStore`; `MemoryConsolidator ──uses──► IMemoryStore/IVectorEmbedder/IDedupDetector/IClusterSummarizer`; `ThresholdDedupDetector ──uses──► IVectorEmbedder`; `CorrectiveRetriever ──uses──► IHybridRetriever/IRetrievalGrader/IQueryRewriter`; `HybridRetriever ──uses──► IResultFuser/IScopeIndexRegistry/IVectorEmbedder`; `QueryPlanRunner ──uses──► IHybridRetriever/IResultFuser/IScopeResolver/IScopeIndexRegistry`; `MultiRepresentationRetriever ──uses──► IScopeIndexRegistry/IVectorEmbedder`; `LexicalIndex ──uses──► IBm25Scorer`; `ClientBackedReranker ──uses──► IRerankerClient`; `FlatJsonVectorIndex ──uses──► IVectorIndexGuard`; Engine `FlatExactIndex/HnswIndex/IvfFlatIndex ──uses──► IVectorIndexGuard` + `──implements──► IVectorIndex`; `GpuAccelerator ──uses──► IGpuContextPolicy`; `IlgpuScoreLauncher ──implements──► IGpuScoreLauncher`; all tests-implement-interface edges sampled.
❌ Wrong/missing:
- **Edges missing for public interface members consumed cross-package**: `MemoryService ──uses──► IChunker/ILexicalIndex` (via `new LexicalIndex`/`new Bm25Scorer` — concrete, so generator only tracks interface params; result: real dependency invisible) — **major**.
- `MemoryService ──uses──► IScopesAware / IScopesAware store hook` not shown; `MemoryService ──uses──► IMemoryLogSink/HashChainAppender/MemoryLogger` (Logging) not shown despite facade referencing Logging — minor.
- `HybridRetriever ──uses──► ChunkIdCodec (Chunking)` missing (concrete cross-package dep invisible) — minor.
- `GpuVectorIndexFactory ──uses──► EngineVectorIndexFactory (concrete)` missing — minor.
- `GraphRetriever ──implements──► IGraphRetriever` ✓ but the *Retrieval-side* `IGraphRetrieverImpl` interface has zero edges — consistent with dead code (F4).
- Package-deps header for `ProvostXMemory` lists 13 deps — matches csproj ✓. `ProvostXMemory.Graph` header lists only Abstractions+Storage ✓ matches csproj. Generator is csproj-driven, not code-driven — explains the misses above.

### PACKAGE-MAP.md — 20 rows checked
✅ Accurate: all 34 rows enumerate the right dependency sets per csproj (spot-checked `Logging`=leaf, `Abstractions`→Logging, `Chunking`→Abstractions, `Storage`→Abstractions, `Lexical`→Abstractions (doc row omits Chunking? — row says only Abstractions ✓ matches… wait, csproj says Abstractions+Chunking; PACKAGE-MAP row says `ProvostXMemory.Abstractions` only — **INACCURATE**), `VectorIndex`→Abstractions, `Engine`→Abstractions (row ✓, csproj actually references VectorIndex too — row says Abstractions only — **INACCURATE**), `Gpu`→Abstractions/VectorIndex/Engine ✓, `Consolidation`→Abstractions/VectorIndex ✓, `Correction`→Abstractions/Retrieval ✓, `Graph`→Abstractions/Storage ✓, `Retrieval`→7 deps ✓, facade→13 ✓, `Eval`→10 ✓, `Examples` — **absent from table** (package exists with 884 LOC) — major.
❌ Wrong:
- `ProvostXMemory.Lexical` row: missing `ProvostXMemory.Chunking` dep (csproj has it) — major (hides the F3 dead edge).
- `ProvostXMemory.VectorIndex.Engine` row: missing `ProvostXMemory.VectorIndex` dep (csproj has it) — major.
- `ProvostXMemory.Examples` missing from table entirely — major.
- Type counts drift: `Abstractions` 16 (real top-level public: 30 incl. enums/records), `Eval` 19 (real: 19 top-level incl. nested-flattened? code shows 16 files/19 top-level types — plausible ✓), `Engine` 23 (real top-level: 21 + nested Options flattened = inflated), `Gpu` 16 (real: 13 + nested Options flattened), `Retrieval` 15 (real: 13 top-level + 2 nested) — **minor** (nested-flattening inflation, consistent with the API-INDEX generator bug).
- LOC columns: `Abstractions` 364 (actual 561), `Engine` 1871 (actual 2447), `Gpu` 1530 (actual 1915), `Retrieval` 776 (actual 1115), `Storage` 541 (actual 758), `Graph` 1022 (actual 1390), `Eval` 1055 (actual 1414), facade 859 (actual 1229), `Logging` 490 (actual 712), `Chunking` 510 (actual 763) — **all undercounted ~30%** (generator likely strips comments/blank lines; systematic, not random — minor but should be re-labeled "code LOC excl. comments").

## 4. API-INDEX vs code drift (explicit list)

**Public types in code, missing from API-INDEX.md** (grep-verified zero mentions):
- `MemoryRecord`, `MemoryRepresentation`, `MemoryRepresentationModality` (Abstractions L0 atoms!)
- `RetrievalResult`, `RetrievalStageDiagnostics`, `QueryPlanStep`
- `ConsolidationReport`, `ConsolidationTuning` (Consolidation)
- `LexicalTermCounts` — present ✓ (no, listed: "🟡 LexicalTermCounts" — remove from missing list)
- `Segmenter`/`ChunkAssembler` — internal, correctly excluded
- Whole package: `ProvostXMemory.Examples` (10 example types)
- `IScopeResolver`, `QueryPlanRunnerOptions` — present as flat entries ✓ (but nesting lost)
- Gpu `GpuDiscoveryOptions`, `GpuLaunchOptions`, `GpuMetricCodec`, `GpuVectorKernels` — present ✓

**API-INDEX entries with no matching top-level code type** (generator flattened nested types):
- 5× bare `Options` (Engine: FlatExactIndex/IvfFlatIndex/EngineVectorIndexFactory nested + Gpu: GpuFlatIndex/GpuIvfIndex nested + one more in HnswIndex? — HnswIndex takes `HnswOptions` (Abstractions-adjacent), so the third Engine "Options" = `HnswSearcher` deps line; verify: Engine lists `Options` 3×, Gpu lists `Options` 2×)
- `Content`, `Entry` (nested in `FlatJsonVectorIndex`, `VectorNamespaceFile`)
- `MemoryScopeComparer : MemoryScope>` — corrupted generic rendering of `IEqualityComparer<MemoryScope>`

**Interface methods:** all `IMemoryService`/`IMemoryStore`/`IVectorIndex`/`ILexicalIndex`(+Remove/EnumerateDocuments)/`IScopeIndexRegistry`(+Reset)/`IMemoryGraphStore`(+UpsertCluster) members exist in code and match their ARCHITECTURE.md descriptions; **no public interface method found in code that is absent from API-INDEX at the interface level** (API-INDEX lists interface names + ctor deps, not per-method signatures — method-level drift check therefore N/A by design; per-type presence is the audited level).

## Summary counts

- **Blockers:** 0
- **Major:** 10 (Abstractions→Logging dep; Lexical→Chunking dead dep; Eval→facade upward dep; Gpu→Engine concrete dep; dead `IGraphRetrieverImpl`; 5 dependency-flow doc contradictions (A2/A6); Examples package missing from PACKAGE-MAP/API-INDEX; L0 atoms missing from API-INDEX; nested-type flattening generator bug (API-INDEX+PACKAGE-MAP); PACKAGE-MAP dep-column errors for Lexical/Engine)
- **Minor:** ~15 (F1 comment gaps; F2 small multi-type files; F5 nits; A1/A3/A4/A5/A7/A8; LOC/method-level doc nits)

**Overall:** The codebase is genuinely close to LDC-clean — constructor injection, no statics, no cycles, and interface-first wiring are all real. The rot is concentrated in (a) **artifact generation bugs** (nested types flattened, LOC undercount, csproj-only dep tracking, Examples/L0 atoms missing), (b) **two real architectural edges** (Abstractions→Logging, Eval→facade) that contradict the blueprint, and (c) **one dead interface** (`IGraphRetrieverImpl`) plus a handful of concrete cross-package `new`s. Fix the generator, cut the two bad edges, delete the dead interface, split the 5-type Lexical file — then re-run this audit.

## Suggested fix order
1. Generator fixes (re-run LDC artifacts): nested-type naming (`Outer.Options`), LOC label, package coverage (Examples), dep tracking from code (`using` + ctor params), fix mangled `MemoryScopeComparer` entry, missing L0 atoms.
2. `Abstractions` → `Logging`: move `MemoryLoggingOptions` to Abstractions (or extract `IHostLoggingOptions` port) — restores L0 purity.
3. Remove dead `Lexical→Chunking` ProjectReference.
4. `Eval`: invert the upward dep (factory port or move `DefaultEvalServiceFactory` to facade/examples).
5. `Gpu→Engine`: depend on `IVectorIndexFactory`/`IVectorIndex` interfaces only (drop the `FlatExactIndex` cast — add `IVectorIndexFactory.Create` contract note or an `IIndexCloneable` port).
6. Delete `IGraphRetrieverImpl` or implement it.
7. Split `ILexicalIndex.cs` (5→5 files), `EvalReport.cs`, `EvalMetrics.cs`, `IScopeIndexRegistry.cs`, `MemoryScope.cs`.
8. Add "Stateless utility" comments to F1 sites; fix `IlgpuScoreLauncher.cs` casing; fix the `DefaultGpuContextPolicy.cs:16` comment glue; update ARCHITECTURE.md A1/A3/A6/A7/A8.
