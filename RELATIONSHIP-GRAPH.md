# RELATIONSHIP-GRAPH.md — ProvostXMemory
Generated: 2026-10-13T17:26:25+00:00
Edges: 93  |  Packages: 35

---

## ProvostXMemory

Package deps: ProvostXMemory.Abstractions, ProvostXMemory.Chunking, ProvostXMemory.Consolidation, ProvostXMemory.Correction, ProvostXMemory.Eval, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Lexical, ProvostXMemory.Logging, ProvostXMemory.Reranking, ProvostXMemory.Retrieval, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine

- MemoryService ──implements──► IMemoryService (ProvostXMemory)

## ProvostXMemory.Abstractions

Package deps: —

- IJsonlMemoryStore ──implements──► IMemoryStore (ProvostXMemory.Abstractions)
- MemoryRecord ──uses──► MemoryScope (ProvostXMemory.Abstractions)
- MemoryRepresentation ──uses──► MemoryRepresentationModality (ProvostXMemory.Abstractions)
- MemoryScope ──uses──► MemoryScopeKind (ProvostXMemory.Abstractions)
- MemoryServiceOptions ──uses──► ConsolidationOptions (ProvostXMemory.Abstractions)
- MemoryServiceOptions ──uses──► FusionOptions (ProvostXMemory.Abstractions)
- MemoryServiceOptions ──uses──► GraphOptions (ProvostXMemory.Abstractions)
- MemoryServiceOptions ──uses──► StorageOptions (ProvostXMemory.Abstractions)
- MemoryServiceOptions ──uses──► VectorOptions (ProvostXMemory.Abstractions)
- RetrievalQuery ──uses──► MemoryScope (ProvostXMemory.Abstractions)
- ScoredMemoryRecord ──uses──► MemoryRecord (ProvostXMemory.Abstractions)
- ScoredMemoryRecord ──uses──► RetrievalProvenance (ProvostXMemory.Abstractions)
- VectorOptions ──uses──► IndexKind (ProvostXMemory.Abstractions)

## ProvostXMemory.AbstractionsTests

Package deps: —


## ProvostXMemory.Chunking

Package deps: ProvostXMemory.Abstractions

- ChunkIdParts ──uses──► MemoryScopeKind (ProvostXMemory.Abstractions) ← CROSS-PKG
- ChunkParentResolver ──implements──► IParentResolver (ProvostXMemory.Chunking)
- SizeChunker ──implements──► IChunker (ProvostXMemory.Chunking)
- StructuredChunker ──implements──► IChunker (ProvostXMemory.Chunking)

## ProvostXMemory.ChunkingTests

Package deps: —

- ChunkParentResolverTests ──implements──► IDisposable (?)

## ProvostXMemory.Consolidation

Package deps: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex

- ClusterSummarizer ──implements──► IClusterSummarizer (ProvostXMemory.Consolidation)
- MemoryConsolidator ──implements──► IMemoryConsolidator (ProvostXMemory.Consolidation)
- ThresholdDedupDetector ──implements──► IDedupDetector (ProvostXMemory.Consolidation)

## ProvostXMemory.ConsolidationTests

Package deps: —

- HashFakeEmbedder ──implements──► IVectorEmbedder (ProvostXMemory.VectorIndex) ← CROSS-PKG
- InMemoryMemoryStore ──implements──► IMemoryStore (ProvostXMemory.Abstractions) ← CROSS-PKG

## ProvostXMemory.Correction

Package deps: ProvostXMemory.Abstractions, ProvostXMemory.Retrieval

- CorrectiveRetriever ──implements──► ICorrectiveRetriever (ProvostXMemory.Retrieval) ← CROSS-PKG
- GradedCandidate ──uses──► RetrievalGrade (ProvostXMemory.Correction)
- GradedCandidate ──uses──► ScoredMemoryRecord (ProvostXMemory.Abstractions) ← CROSS-PKG
- KeywordQueryRewriter ──implements──► IQueryRewriter (ProvostXMemory.Correction)

## ProvostXMemory.CorrectionTests

Package deps: —

- RecordingQueryRewriter ──implements──► IQueryRewriter (ProvostXMemory.Correction) ← CROSS-PKG
- StubExternalFetcher ──implements──► IExternalFetcher (ProvostXMemory.Correction) ← CROSS-PKG
- StubHybridRetriever ──implements──► IHybridRetriever (ProvostXMemory.Retrieval) ← CROSS-PKG
- StubRetrievalGrader ──implements──► IRetrievalGrader (ProvostXMemory.Correction) ← CROSS-PKG

## ProvostXMemory.Eval

Package deps: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex

- ClusterHashEmbedder ──implements──► IVectorEmbedder (ProvostXMemory.VectorIndex) ← CROSS-PKG
- EvalMetricBreakdownEntry ──uses──► EvalMetrics (ProvostXMemory.Eval)
- EvalReport ──uses──► EvalMetrics (ProvostXMemory.Eval)
- EvalReport ──uses──► MetricThresholdViolations (ProvostXMemory.Eval)
- MemoryScopeComparer ──implements──► IEqualityComparer<Abstractions.MemoryScope> (?)

## ProvostXMemory.EvalTests

Package deps: —

- EvalRunnerTests ──implements──► IDisposable (?)

## ProvostXMemory.Examples

Package deps: ProvostXMemory, ProvostXMemory.Eval, ProvostXMemory.VectorIndex.Engine, ProvostXMemory.VectorIndex.Gpu


## ProvostXMemory.Fusion

Package deps: ProvostXMemory.Abstractions

- ReciprocalRankFuser ──implements──► IResultFuser (ProvostXMemory.Fusion)

## ProvostXMemory.FusionTests

Package deps: —


## ProvostXMemory.Graph

Package deps: ProvostXMemory.Abstractions, ProvostXMemory.Storage

- GraphRetriever ──implements──► IGraphRetriever (ProvostXMemory.Graph)
- HeuristicEntityExtractor ──implements──► IEntityExtractor (ProvostXMemory.Graph)
- JsonlMemoryGraphStore ──implements──► IMemoryGraphStore (ProvostXMemory.Graph)

## ProvostXMemory.GraphTests

Package deps: —


## ProvostXMemory.Lexical

Package deps: ProvostXMemory.Abstractions

- Bm25Scorer ──implements──► IBm25Scorer (ProvostXMemory.Lexical)
- LexicalIndex ──implements──► ILexicalIndex (ProvostXMemory.Lexical)

## ProvostXMemory.LexicalTests

Package deps: —


## ProvostXMemory.Logging

Package deps: ProvostXMemory.Abstractions

- ConsoleMemoryLogSink ──implements──► IMemoryLogSink (ProvostXMemory.Logging)
- FileMemoryLogSink ──implements──► IMemoryLogSink (ProvostXMemory.Logging)
- MemoryLogEntry ──uses──► MemoryLogCategory (ProvostXMemory.Logging)
- MemoryLogEntry ──uses──► MemoryLogLevel (ProvostXMemory.Abstractions) ← CROSS-PKG

## ProvostXMemory.LoggingTests

Package deps: —

- RecordingSink ──implements──► IMemoryLogSink (ProvostXMemory.Logging) ← CROSS-PKG

## ProvostXMemory.Reranking

Package deps: ProvostXMemory.Abstractions

- ClientBackedReranker ──implements──► IReranker (ProvostXMemory.Reranking)
- NoopReranker ──implements──► IReranker (ProvostXMemory.Reranking)
- ScoreBoostReranker ──implements──► IReranker (ProvostXMemory.Reranking)

## ProvostXMemory.RerankingTests

Package deps: —

- StubRerankerClient ──implements──► IRerankerClient (ProvostXMemory.Reranking) ← CROSS-PKG

## ProvostXMemory.Retrieval

Package deps: ProvostXMemory.Abstractions, ProvostXMemory.Chunking, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Lexical, ProvostXMemory.Reranking, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex

- HybridRetriever ──implements──► IHybridRetriever (ProvostXMemory.Retrieval)
- InMemoryScopeIndexRegistry ──implements──► IScopeIndexRegistry (ProvostXMemory.Retrieval)
- KeywordQueryTextRewriter ──implements──► IQueryTextRewriter (ProvostXMemory.Retrieval)
- MultiRepresentationRetriever ──implements──► IMultiRepresentationRetriever (ProvostXMemory.Retrieval)
- QueryPlanRunner ──implements──► IQueryPlanRunner (ProvostXMemory.Retrieval)

## ProvostXMemory.RetrievalTests

Package deps: —

- HashEmbedder ──implements──► IVectorEmbedder (ProvostXMemory.VectorIndex) ← CROSS-PKG
- InMemoryStore ──implements──► IMemoryStore (ProvostXMemory.Abstractions) ← CROSS-PKG

## ProvostXMemory.Storage

Package deps: ProvostXMemory.Abstractions

- JsonlMemoryStore ──implements──► IJsonlMemoryStore (ProvostXMemory.Abstractions) ← CROSS-PKG
- MemoryManifestCodec ──implements──► IMemoryManifestCodec (ProvostXMemory.Abstractions) ← CROSS-PKG

## ProvostXMemory.StorageTests

Package deps: —

- JsonlMemoryStoreTests ──implements──► IDisposable (?)
- MemoryManifestCodecTests ──implements──► IDisposable (?)
- ScopeIsolationTests ──implements──► IDisposable (?)

## ProvostXMemory.SystemTests

Package deps: —

- ConsolidationAndGraphTests ──implements──► IDisposable (?)
- KnowledgeAssistantJourneyTests ──implements──► IDisposable (?)
- LifecycleJourneyTests ──implements──► IDisposable (?)
- MultiTenantJourneyTests ──implements──► IDisposable (?)
- PersistenceAndRoutingTests ──implements──► IDisposable (?)
- TempRoot ──implements──► IDisposable (?)

## ProvostXMemory.Telemetry

Package deps: ProvostXMemory.Abstractions

- InMemoryMemoryTelemetry ──implements──► IMemoryTelemetry (ProvostXMemory.Telemetry)
- NoopMemoryTelemetry ──implements──► IMemoryTelemetry (ProvostXMemory.Telemetry)

## ProvostXMemory.TelemetryTests

Package deps: —


## ProvostXMemory.VectorIndex

Package deps: ProvostXMemory.Abstractions

- FlatJsonVectorIndex ──implements──► IVectorIndex (ProvostXMemory.VectorIndex)

## ProvostXMemory.VectorIndex.Engine

Package deps: ProvostXMemory.VectorIndex

- EngineVectorIndexFactory ──implements──► IVectorIndexFactory (ProvostXMemory.VectorIndex) ← CROSS-PKG
- FlatExactIndex ──implements──► IVectorIndex (ProvostXMemory.VectorIndex) ← CROSS-PKG
- HnswIndex ──implements──► IVectorIndex (ProvostXMemory.VectorIndex) ← CROSS-PKG
- HnswPublishedGraph ──uses──► HnswGraph (ProvostXMemory.VectorIndex.Engine)
- IvfFlatIndex ──implements──► IVectorIndex (ProvostXMemory.VectorIndex) ← CROSS-PKG

## ProvostXMemory.VectorIndex.Gpu

Package deps: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex

- DefaultGpuContextPolicy ──implements──► IGpuContextPolicy (ProvostXMemory.VectorIndex.Gpu)
- GpuAccelerator ──implements──► IGpuAccelerator (ProvostXMemory.VectorIndex.Gpu)
- GpuFlatIndex ──implements──► IVectorIndex (ProvostXMemory.VectorIndex) ← CROSS-PKG
- GpuIvfIndex ──implements──► IVectorIndex (ProvostXMemory.VectorIndex) ← CROSS-PKG
- GpuVectorIndexFactory ──implements──► IVectorIndexFactory (ProvostXMemory.VectorIndex) ← CROSS-PKG
- GpuVectorMatrix ──implements──► IDisposable (?)
- IGpuAccelerator ──implements──► IDisposable (?)
- IlgpuScoreLauncher ──implements──► IGpuScoreLauncher (ProvostXMemory.VectorIndex.Gpu)

## ProvostXMemory.VectorIndex.GpuTests

Package deps: ProvostXMemory.VectorIndexTests


## ProvostXMemory.VectorIndexTests

Package deps: —


## ProvostXMemoryTests

Package deps: —

- FacadeRecordingLogSink ──implements──► IMemoryLogSink (ProvostXMemory.Logging) ← CROSS-PKG
- FacadeStore ──implements──► IMemoryStore (ProvostXMemory.Abstractions) ← CROSS-PKG
- GraphIndexLedgerClusterTests ──implements──► IDisposable (?)
- HashEmbedder ──implements──► IVectorEmbedder (ProvostXMemory.VectorIndex) ← CROSS-PKG
- MemoryServiceLoggingTests ──implements──► IDisposable (?)
- MemoryServiceTests ──implements──► IDisposable (?)

