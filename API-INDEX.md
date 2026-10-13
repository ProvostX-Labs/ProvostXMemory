# API-INDEX.md — ProvostXMemory
Generated: 2026-10-13T17:26:25+00:00
Packages: 35  |  Types: 322

---

## ProvostXMemory (11 types, 1196 code LOC excl. comments)

- 🟡 DefaultEvalServiceFactory : EvalServiceFactory  (ProvostXMemory)
- 🟡 EvalRunner  (ProvostXMemory)
- 🟡 EvalRunnerOptions  (ProvostXMemory)
- 🔵 EvalServiceFactory  (ProvostXMemory)
- 🟡 GraphIndexLedger  (ProvostXMemory)
- 🔵 IMemoryService  (ProvostXMemory)
- 🔵 IScopesAware  (ProvostXMemory)
- 🟡 MemoryService : IMemoryService  (ProvostXMemory)
- 🟡 MemoryServiceBuilder  (ProvostXMemory)
- 🟣 MemoryServicePipelineOptions  (ProvostXMemory)
- 🟡 RootChildScopeResolver : QueryPlanRunner.IScopeResolver  (ProvostXMemory)

## ProvostXMemory.Abstractions (35 types, 410 code LOC excl. comments)

- 🟣 BackendStats  (ProvostXMemory.Abstractions)  deps: [long, long]
- 🟣 Chunk  (ProvostXMemory.Abstractions)  deps: [string, string, string, int, int, int]
- 🟣 ChunkOptions  (ProvostXMemory.Abstractions)  deps: [int, int, bool, int]
- 🟣 ConsolidationOptions  (ProvostXMemory.Abstractions)  deps: [double, bool, bool]
- 🟣 FusionOptions  (ProvostXMemory.Abstractions)  deps: [double, double, double]
- 🟣 GraphOptions  (ProvostXMemory.Abstractions)  deps: [bool, int, int, int]
- 🔵 IJsonlMemoryStore : IMemoryStore  (ProvostXMemory.Abstractions)
- 🔵 IMemoryManifestCodec  (ProvostXMemory.Abstractions)
- 🔵 IMemoryStore  (ProvostXMemory.Abstractions)
- 🟠 IndexKind  (ProvostXMemory.Abstractions)
- 🟣 MemoryEntryModel  (ProvostXMemory.Abstractions)  deps: [string, string, string, string, string, IReadOnlyList<string>, long, long]
- 🟠 MemoryLogFormat  (ProvostXMemory.Abstractions)
- 🟠 MemoryLogIntegrity  (ProvostXMemory.Abstractions)
- 🟠 MemoryLogLevel  (ProvostXMemory.Abstractions)
- 🟠 MemoryLogSinkKind  (ProvostXMemory.Abstractions)
- 🟣 MemoryLoggingOptions  (ProvostXMemory.Abstractions)
- 🟣 MemoryManifest  (ProvostXMemory.Abstractions)  deps: [string, long, long]
- 🟣 MemoryRecord  (ProvostXMemory.Abstractions)  deps: [string, MemoryScope, string, string]
- 🟣 MemoryRepresentation  (ProvostXMemory.Abstractions)  deps: [MemoryRepresentationModality, string]
- 🟠 MemoryRepresentationModality  (ProvostXMemory.Abstractions)
- 🟣 MemoryScope  (ProvostXMemory.Abstractions)  deps: [MemoryScopeKind, string]
- 🟠 MemoryScopeKind  (ProvostXMemory.Abstractions)
- 🟣 MemoryServiceOptions  (ProvostXMemory.Abstractions)  deps: [StorageOptions, VectorOptions, GraphOptions, FusionOptions, ConsolidationOptions]
- 🟣 QueryPlan  (ProvostXMemory.Abstractions)  deps: [string]
- 🟣 QueryPlanStep  (ProvostXMemory.Abstractions)  deps: [string, string, int]
- 🟡 QueryTokens  (ProvostXMemory.Abstractions)
- 🟣 RetrievalFilters  (ProvostXMemory.Abstractions)
- 🟣 RetrievalProvenance  (ProvostXMemory.Abstractions)  deps: [string]
- 🟣 RetrievalQuery  (ProvostXMemory.Abstractions)  deps: [string, MemoryScope, int]
- 🟣 RetrievalResult  (ProvostXMemory.Abstractions)  deps: [IReadOnlyList<ScoredMemoryRecord>]
- 🟣 RetrievalStageDiagnostics  (ProvostXMemory.Abstractions)  deps: [string, TimeSpan]
- 🟣 ScoredId  (ProvostXMemory.Abstractions)  deps: [string, string, string, double]
- 🟣 ScoredMemoryRecord  (ProvostXMemory.Abstractions)  deps: [MemoryRecord, double, RetrievalProvenance]
- 🟣 StorageOptions  (ProvostXMemory.Abstractions)  deps: [string, bool, bool]
- 🟣 VectorOptions  (ProvostXMemory.Abstractions)  deps: [string, int, IndexKind, int]

## ProvostXMemory.AbstractionsTests (2 types, 65 code LOC excl. comments)

- 🟡 QueryTokensTests  (ProvostXMemory.AbstractionsTests)
- 🟡 ScaffoldPlaceholderTests  (ProvostXMemory.AbstractionsTests)

## ProvostXMemory.Chunking (11 types, 523 code LOC excl. comments)

- 🟡 ChunkAssembler  (ProvostXMemory.Chunking)
- 🟡 ChunkIdCodec  (ProvostXMemory.Chunking)
- 🟣 ChunkIdParts  (ProvostXMemory.Chunking)  deps: [MemoryScopeKind, string, string, int]
- 🟡 ChunkParentResolver : IParentResolver  (ProvostXMemory.Chunking)
- 🔵 IChunker  (ProvostXMemory.Chunking)
- 🔵 IParentResolver  (ProvostXMemory.Chunking)
- 🟡 Segmenter  (ProvostXMemory.Chunking)
- 🟡 SizeChunker : IChunker  (ProvostXMemory.Chunking)
- 🟡 StructuredChunker : IChunker  (ProvostXMemory.Chunking)
- 🟣 struct  (ProvostXMemory.Chunking)  deps: [int, int]
- 🟣 struct  (ProvostXMemory.Chunking)  deps: [string, int, int]

## ProvostXMemory.ChunkingTests (5 types, 657 code LOC excl. comments)

- 🟡 ChunkIdCodecTests  (ProvostXMemory.ChunkingTests)
- 🟡 ChunkParentResolverTests : IDisposable  (ProvostXMemory.ChunkingTests)
- 🟡 ScaffoldPlaceholderTests  (ProvostXMemory.ChunkingTests)
- 🟡 SizeChunkerTests  (ProvostXMemory.ChunkingTests)
- 🟡 StructuredChunkerTests  (ProvostXMemory.ChunkingTests)

## ProvostXMemory.Consolidation (9 types, 475 code LOC excl. comments)

- 🟡 ClusterSummarizer : IClusterSummarizer  (ProvostXMemory.Consolidation)
- 🟣 ConsolidationReport  (ProvostXMemory.Consolidation)
- 🟡 ConsolidationReportMeta  (ProvostXMemory.Consolidation)
- 🟣 ConsolidationTuning  (ProvostXMemory.Consolidation)
- 🔵 IClusterSummarizer  (ProvostXMemory.Consolidation)
- 🔵 IDedupDetector  (ProvostXMemory.Consolidation)
- 🔵 IMemoryConsolidator  (ProvostXMemory.Consolidation)
- 🟡 MemoryConsolidator : IMemoryConsolidator  (ProvostXMemory.Consolidation)
- 🟡 ThresholdDedupDetector : IDedupDetector  (ProvostXMemory.Consolidation)

## ProvostXMemory.ConsolidationTests (7 types, 559 code LOC excl. comments)

- 🟡 ClusterSummarizerTests  (ProvostXMemory.ConsolidationTests)
- 🟡 ConsolidationReportTests  (ProvostXMemory.ConsolidationTests)
- 🟡 HashFakeEmbedder : IVectorEmbedder  (ProvostXMemory.ConsolidationTests)
- 🟡 InMemoryMemoryStore : IMemoryStore  (ProvostXMemory.ConsolidationTests)
- 🟡 MemoryConsolidatorTests  (ProvostXMemory.ConsolidationTests)
- 🟡 TestRecords  (ProvostXMemory.ConsolidationTests)
- 🟡 ThresholdDedupDetectorTests  (ProvostXMemory.ConsolidationTests)

## ProvostXMemory.Correction (8 types, 178 code LOC excl. comments)

- 🟣 CorrectionOptions  (ProvostXMemory.Correction)  deps: [double, bool]
- 🟡 CorrectiveRetriever : ICorrectiveRetriever  (ProvostXMemory.Correction)
  - CorrectiveRetriever.StageNames  (ProvostXMemory.Correction) [nested]
- 🟣 GradedCandidate  (ProvostXMemory.Correction)  deps: [ScoredMemoryRecord, RetrievalGrade]
- 🔵 IExternalFetcher  (ProvostXMemory.Correction)
- 🔵 IQueryRewriter  (ProvostXMemory.Correction)
- 🔵 IRetrievalGrader  (ProvostXMemory.Correction)
- 🟡 KeywordQueryRewriter : IQueryRewriter  (ProvostXMemory.Correction)
- 🟠 RetrievalGrade  (ProvostXMemory.Correction)

## ProvostXMemory.CorrectionTests (7 types, 469 code LOC excl. comments)

- 🟡 CorrectiveRetrieverTests  (ProvostXMemory.CorrectionTests)
- 🟡 Fixtures  (ProvostXMemory.CorrectionTests)
- 🟡 KeywordQueryRewriterTests  (ProvostXMemory.CorrectionTests)
- 🟡 RecordingQueryRewriter : IQueryRewriter  (ProvostXMemory.CorrectionTests)
- 🟡 StubExternalFetcher : IExternalFetcher  (ProvostXMemory.CorrectionTests)
- 🟡 StubHybridRetriever : IHybridRetriever  (ProvostXMemory.CorrectionTests)
- 🟡 StubRetrievalGrader : IRetrievalGrader  (ProvostXMemory.CorrectionTests)

## ProvostXMemory.Eval (16 types, 784 code LOC excl. comments)

- 🟡 ClusterHashEmbedder : IVectorEmbedder  (ProvostXMemory.Eval)
- 🟡 DeterministicEvalCorpus  (ProvostXMemory.Eval)
  - DeterministicEvalCorpus.CorpusRecord  (ProvostXMemory.Eval) [nested]
- 🟡 EvalCase  (ProvostXMemory.Eval)
- 🟣 EvalCaseMetrics  (ProvostXMemory.Eval)  deps: [string, string, string, double, double, double, double, int, int, int]
- 🟡 EvalCaseSet  (ProvostXMemory.Eval)
- 🟣 EvalCaseTrace  (ProvostXMemory.Eval)  deps: [string, string, string, int, IReadOnlyList<EvalCaseTraceHit>]
- 🟣 EvalCaseTraceHit  (ProvostXMemory.Eval)  deps: [string, double]
- 🟣 EvalMetricBreakdownEntry  (ProvostXMemory.Eval)  deps: [string, EvalMetrics, IReadOnlyList<string>]
- 🟡 EvalMetricMath  (ProvostXMemory.Eval)
- 🟡 EvalMetricThresholds  (ProvostXMemory.Eval)
- 🟣 EvalMetrics  (ProvostXMemory.Eval)  deps: [double, double, double, double]
- 🟣 EvalReport  (ProvostXMemory.Eval)  deps: [string, int, EvalMetrics, IReadOnlyList<EvalCaseMetrics>, IReadOnlyList<EvalMetricBreakdownEntry>, IReadOnlyList<EvalMetricBreakdownEntry>, MetricThresholdViolations, IReadOnlyList<EvalCaseTrace>, long, double]
- 🟡 EvalReportJson  (ProvostXMemory.Eval)
- 🟡 MemoryScopeComparer : IEqualityComparer<Abstractions.MemoryScope>  (ProvostXMemory.Eval)
- 🟣 MetricThresholdViolation  (ProvostXMemory.Eval)  deps: [string, double, double]
- 🟡 MetricThresholdViolations  (ProvostXMemory.Eval)

## ProvostXMemory.EvalTests (7 types, 752 code LOC excl. comments)

- 🟡 ClusterHashEmbedderTests  (ProvostXMemory.EvalTests)
- 🟡 DefaultEvalServiceFactoryTests  (ProvostXMemory.EvalTests)
- 🟡 DeterministicEvalCorpusTests  (ProvostXMemory.EvalTests)
- 🟡 EvalMetricMathTests  (ProvostXMemory.EvalTests)
- 🟡 EvalModelTests  (ProvostXMemory.EvalTests)
- 🟡 EvalRunnerTests : IDisposable  (ProvostXMemory.EvalTests)
- 🟡 RecallRegressionGuardTests  (ProvostXMemory.EvalTests)

## ProvostXMemory.Examples (13 types, 636 code LOC excl. comments)

- 🟡 Ex01_Quickstart : ExampleBase  (ProvostXMemory.Examples)
- 🟡 Ex02_Scopes : ExampleBase  (ProvostXMemory.Examples)
- 🟡 Ex03_HybridSearch : ExampleBase  (ProvostXMemory.Examples)
- 🟡 Ex04_Backends : ExampleBase  (ProvostXMemory.Examples)
- 🟡 Ex05_Consolidation : ExampleBase  (ProvostXMemory.Examples)
- 🟡 Ex06_GraphRetrieval : ExampleBase  (ProvostXMemory.Examples)
- 🟡 Ex07_CorrectiveLoop : ExampleBase  (ProvostXMemory.Examples)
- 🟡 Ex08_LoggingAudit : ExampleBase  (ProvostXMemory.Examples)
- 🟡 Ex09_EvalHarness : ExampleBase  (ProvostXMemory.Examples)
- 🟡 Ex10_RestartPersistence : ExampleBase  (ProvostXMemory.Examples)
- 🟡 ExampleBase  (ProvostXMemory.Examples)
- 🟡 ExampleRig  (ProvostXMemory.Examples)
  - ExampleRig.FlatJsonFactory  (ProvostXMemory.Examples) [nested]
- 🟡 Program  (ProvostXMemory.Examples)

## ProvostXMemory.Fusion (3 types, 92 code LOC excl. comments)

- 🔵 IResultFuser  (ProvostXMemory.Fusion)
- 🟣 RankedList  (ProvostXMemory.Fusion)  deps: [string, IReadOnlyList<ScoredId>]
- 🟡 ReciprocalRankFuser : IResultFuser  (ProvostXMemory.Fusion)

## ProvostXMemory.FusionTests (1 types, 254 code LOC excl. comments)

- 🟡 ReciprocalRankFuserTests  (ProvostXMemory.FusionTests)

## ProvostXMemory.Graph (11 types, 1022 code LOC excl. comments)

- 🟣 ExtractionResult  (ProvostXMemory.Graph)  deps: [IReadOnlyList<GraphEntity>, IReadOnlyList<GraphRelation>]
- 🟣 GraphCluster  (ProvostXMemory.Graph)  deps: [string, IReadOnlyList<string>]
- 🟣 GraphEntity  (ProvostXMemory.Graph)  deps: [string, string, IReadOnlyList<string>, IReadOnlyList<string>]
- 🟣 GraphRelation  (ProvostXMemory.Graph)  deps: [string, string, string, string, IReadOnlyList<string>]
- 🟡 GraphRetriever : IGraphRetriever  (ProvostXMemory.Graph)
- 🟡 HeuristicEntityExtractor : IEntityExtractor  (ProvostXMemory.Graph)
- 🔵 IEntityExtractor  (ProvostXMemory.Graph)
- 🔵 IGraphRetriever  (ProvostXMemory.Graph)
- 🔵 IMemoryGraphStore  (ProvostXMemory.Graph)
- 🔵 IMemoryGraphStoreResolver  (ProvostXMemory.Graph)
- 🟡 JsonlMemoryGraphStore : IMemoryGraphStore  (ProvostXMemory.Graph)

## ProvostXMemory.GraphTests (4 types, 559 code LOC excl. comments)

- 🟡 GraphRetrieverTests  (ProvostXMemory.GraphTests)
- 🟡 GraphShortestPathTests  (ProvostXMemory.GraphTests)
- 🟡 HeuristicEntityExtractorTests  (ProvostXMemory.GraphTests)
- 🟡 JsonlMemoryGraphStoreTests  (ProvostXMemory.GraphTests)

## ProvostXMemory.Lexical (11 types, 423 code LOC excl. comments)

- 🟡 Bm25Scorer : IBm25Scorer  (ProvostXMemory.Lexical)
- 🟣 CorpusStatistics  (ProvostXMemory.Lexical)  deps: [int]
- 🔵 IBm25Scorer  (ProvostXMemory.Lexical)
- 🔵 ILexicalIndex  (ProvostXMemory.Lexical)
- 🟣 LexicalDocument  (ProvostXMemory.Lexical)  deps: [string, IReadOnlyList<string>]
- 🟡 LexicalIndex : ILexicalIndex  (ProvostXMemory.Lexical)
- 🟣 LexicalPosting  (ProvostXMemory.Lexical)  deps: [string, int]
- 🟡 LexicalTermCounts  (ProvostXMemory.Lexical)
- 🟡 LexicalTokenizer  (ProvostXMemory.Lexical)
- 🟣 ScoredLexicalId  (ProvostXMemory.Lexical)  deps: [string, double]
- 🟣 TermStat  (ProvostXMemory.Lexical)  deps: [string, int, int]

## ProvostXMemory.LexicalTests (3 types, 479 code LOC excl. comments)

- 🟡 Bm25ScorerTests  (ProvostXMemory.LexicalTests)
- 🟡 LexicalIndexTests  (ProvostXMemory.LexicalTests)
- 🟡 LexicalTokenizerTests  (ProvostXMemory.LexicalTests)

## ProvostXMemory.Logging (13 types, 464 code LOC excl. comments)

- 🟡 ConsoleMemoryLogSink : IMemoryLogSink  (ProvostXMemory.Logging)
- 🟡 FileMemoryLogSink : IMemoryLogSink  (ProvostXMemory.Logging)
- 🟡 HashChainAppender  (ProvostXMemory.Logging)
- 🔵 IMemoryLogSink  (ProvostXMemory.Logging)
- 🟣 LogChainVerificationResult  (ProvostXMemory.Logging)  deps: [bool]
- 🟡 LogChainVerifier  (ProvostXMemory.Logging)
- 🟠 MemoryLogCategory  (ProvostXMemory.Logging)
- 🟣 MemoryLogEntry  (ProvostXMemory.Logging)  deps: [DateTimeOffset, long, MemoryLogLevel, MemoryLogCategory, string]
- 🟡 MemoryLogJson  (ProvostXMemory.Logging)
- 🟣 MemoryLogJsonLine  (ProvostXMemory.Logging)
- 🟣 MemoryLogScope  (ProvostXMemory.Logging)  deps: [string, string]
- 🟡 MemoryLogger  (ProvostXMemory.Logging)
- 🟡 RetentionSweeper  (ProvostXMemory.Logging)

## ProvostXMemory.LoggingTests (7 types, 496 code LOC excl. comments)

- 🟡 ContentLeakTests  (ProvostXMemory.LoggingTests)
- 🟡 FileRollingTests  (ProvostXMemory.LoggingTests)
- 🟡 HashChainTests  (ProvostXMemory.LoggingTests)
- 🟡 MemoryLoggerDisabledTests  (ProvostXMemory.LoggingTests)
- 🟡 MemoryLoggerLevelTests  (ProvostXMemory.LoggingTests)
- 🟡 RecordingSink : IMemoryLogSink  (ProvostXMemory.LoggingTests)
- 🟡 RetentionSweeperTests  (ProvostXMemory.LoggingTests)

## ProvostXMemory.Reranking (7 types, 222 code LOC excl. comments)

- 🟡 ClientBackedReranker : IReranker  (ProvostXMemory.Reranking)
- 🟣 ClientBackedRerankerOptions  (ProvostXMemory.Reranking)  deps: [bool]
- 🔵 IReranker  (ProvostXMemory.Reranking)
- 🔵 IRerankerClient  (ProvostXMemory.Reranking)
- 🟡 NoopReranker : IReranker  (ProvostXMemory.Reranking)
- 🟡 ScoreBoostReranker : IReranker  (ProvostXMemory.Reranking)
- 🟣 ScoreBoostRerankerOptions  (ProvostXMemory.Reranking)  deps: [double, double, double, double, double, int, int]

## ProvostXMemory.RerankingTests (5 types, 363 code LOC excl. comments)

- 🟡 ClientBackedRerankerTests  (ProvostXMemory.RerankingTests)
- 🟡 Fixtures  (ProvostXMemory.RerankingTests)
- 🟡 NoopRerankerTests  (ProvostXMemory.RerankingTests)
- 🟡 ScoreBoostRerankerTests  (ProvostXMemory.RerankingTests)
- 🟡 StubRerankerClient : IRerankerClient  (ProvostXMemory.RerankingTests)

## ProvostXMemory.Retrieval (12 types, 809 code LOC excl. comments)

- 🟡 HybridRetriever : IHybridRetriever  (ProvostXMemory.Retrieval)
- 🔵 ICorrectiveRetriever  (ProvostXMemory.Retrieval)
- 🔵 IHybridRetriever  (ProvostXMemory.Retrieval)
- 🔵 IMultiRepresentationRetriever  (ProvostXMemory.Retrieval)
- 🔵 IQueryPlanRunner  (ProvostXMemory.Retrieval)
- 🔵 IQueryTextRewriter  (ProvostXMemory.Retrieval)
- 🔵 IScopeIndexRegistry  (ProvostXMemory.Retrieval)
- 🟡 InMemoryScopeIndexRegistry : IScopeIndexRegistry  (ProvostXMemory.Retrieval)
- 🟡 KeywordQueryTextRewriter : IQueryTextRewriter  (ProvostXMemory.Retrieval)
- 🟡 MultiRepresentationRetriever : IMultiRepresentationRetriever  (ProvostXMemory.Retrieval)
- 🟡 QueryPlanRunner : IQueryPlanRunner  (ProvostXMemory.Retrieval)
  - QueryPlanRunner.IScopeResolver  (ProvostXMemory.Retrieval) [nested]
  - QueryPlanRunner.QueryPlanRunnerOptions  (ProvostXMemory.Retrieval) [nested]
- 🟡 RetrievalFilterApplier  (ProvostXMemory.Retrieval)

## ProvostXMemory.RetrievalTests (7 types, 830 code LOC excl. comments)

- 🟡 HashEmbedder : IVectorEmbedder  (ProvostXMemory.RetrievalTests)
- 🟡 HybridRetrieverTests  (ProvostXMemory.RetrievalTests)
- 🟡 InMemoryStore : IMemoryStore  (ProvostXMemory.RetrievalTests)
- 🟡 MultiRepresentationRetrieverTests  (ProvostXMemory.RetrievalTests)
- 🟡 QueryPlanRunnerTests  (ProvostXMemory.RetrievalTests)
- 🟡 ScopeIndexRegistryTests  (ProvostXMemory.RetrievalTests)
- 🟡 ScopeIndexRig  (ProvostXMemory.RetrievalTests)

## ProvostXMemory.Storage (4 types, 541 code LOC excl. comments)

- 🟡 AtomicFileWriter  (ProvostXMemory.Storage)
  - AtomicFileWriter.WriteResult  (ProvostXMemory.Storage) [nested]
- 🟡 JsonlMemoryStore : IJsonlMemoryStore  (ProvostXMemory.Storage)
- 🟡 MemoryManifestCodec : IMemoryManifestCodec  (ProvostXMemory.Storage)
- 🟡 MemoryRootLayout  (ProvostXMemory.Storage)

## ProvostXMemory.StorageTests (6 types, 633 code LOC excl. comments)

- 🟡 AtomicFileWriterTests  (ProvostXMemory.StorageTests)
- 🟡 JsonlMemoryStoreTests : IDisposable  (ProvostXMemory.StorageTests)
- 🟡 MemoryManifestCodecTests : IDisposable  (ProvostXMemory.StorageTests)
- 🟡 MemoryRootLayoutTests  (ProvostXMemory.StorageTests)
- 🟡 ScaffoldPlaceholderTests  (ProvostXMemory.StorageTests)
- 🟡 ScopeIsolationTests : IDisposable  (ProvostXMemory.StorageTests)

## ProvostXMemory.SystemTests (16 types, 1829 code LOC excl. comments)

- 🟡 BackendCompositionTests  (ProvostXMemory.SystemTests.Integration)
- 🟡 ChunkOptionsEdgeTests  (ProvostXMemory.SystemTests.Unit)
- 🟡 ConsolidationAndGraphTests : IDisposable  (ProvostXMemory.SystemTests.Integration)
- 🟡 EvalReportRoundTripTests  (ProvostXMemory.SystemTests.Unit)
- 🟡 FusionWeightTests  (ProvostXMemory.SystemTests.Unit)
- 🟡 HnswDeterminismTests  (ProvostXMemory.SystemTests.Unit)
- 🟡 KnowledgeAssistantJourneyTests : IDisposable  (ProvostXMemory.SystemTests.Journey)
- 🟡 LifecycleJourneyTests : IDisposable  (ProvostXMemory.SystemTests.Journey)
- 🟡 MemoryScopeSemanticsTests  (ProvostXMemory.SystemTests.Unit)
- 🟡 MultiTenantJourneyTests : IDisposable  (ProvostXMemory.SystemTests.Journey)
- 🟡 PersistenceAndRoutingTests : IDisposable  (ProvostXMemory.SystemTests.Integration)
- 🟡 RetrievalFiltersEnforcementTests  (ProvostXMemory.SystemTests.Integration)
- 🟡 RetrievalQueryFilterTests  (ProvostXMemory.SystemTests.Unit)
- 🟡 SystemRig  (ProvostXMemory.SystemTests)
  - SystemRig.CaptureFactory  (ProvostXMemory.SystemTests) [nested]
  - SystemRig.FlatJsonFactory  (ProvostXMemory.SystemTests) [nested]
- 🟡 TempRoot : IDisposable  (ProvostXMemory.SystemTests)
- 🟡 TopicCorpus  (ProvostXMemory.SystemTests)

## ProvostXMemory.Telemetry (3 types, 118 code LOC excl. comments)

- 🔵 IMemoryTelemetry  (ProvostXMemory.Telemetry)
- 🟡 InMemoryMemoryTelemetry : IMemoryTelemetry  (ProvostXMemory.Telemetry)
- 🟡 NoopMemoryTelemetry : IMemoryTelemetry  (ProvostXMemory.Telemetry)

## ProvostXMemory.TelemetryTests (3 types, 115 code LOC excl. comments)

- 🟡 InMemoryMemoryTelemetryTests  (ProvostXMemory.TelemetryTests)
- 🟡 NoopMemoryTelemetryTests  (ProvostXMemory.TelemetryTests)
- 🟡 ScaffoldPlaceholderTests  (ProvostXMemory.TelemetryTests)

## ProvostXMemory.VectorIndex (22 types, 1115 code LOC excl. comments)

- 🟡 FlatJsonVectorIndex : IVectorIndex  (ProvostXMemory.VectorIndex)
- 🔵 IVectorEmbedder  (ProvostXMemory.VectorIndex)
- 🔵 IVectorIndex  (ProvostXMemory.VectorIndex)
- 🔵 IVectorIndexFactory  (ProvostXMemory.VectorIndex)
- 🔵 IVectorIndexGuard  (ProvostXMemory.VectorIndex)
- 🟣 IvfMember  (ProvostXMemory.VectorIndex)  deps: [string, float[], int]
- 🟡 IvfPostingLists  (ProvostXMemory.VectorIndex)
- 🟡 KMeansQuantizer  (ProvostXMemory.VectorIndex)
- 🟡 KMeansResult  (ProvostXMemory.VectorIndex)
- 🟡 ScalarVectorOps  (ProvostXMemory.VectorIndex)
- 🟣 ScoredVectorId  (ProvostXMemory.VectorIndex)  deps: [string, float]
- 🟡 SeededRandom  (ProvostXMemory.VectorIndex)
- 🟠 VectorDistanceMetric  (ProvostXMemory.VectorIndex)
- 🟣 VectorIndexGuard  (ProvostXMemory.VectorIndex)  deps: [string, int]
- 🟡 VectorIndexGuardMismatchException : Exception  (ProvostXMemory.VectorIndex)
- 🟡 VectorIndexIdNotFoundException : Exception  (ProvostXMemory.VectorIndex)
- 🟣 VectorItem  (ProvostXMemory.VectorIndex)  deps: [string, IReadOnlyList<float>]
- 🟡 VectorItemStore  (ProvostXMemory.VectorIndex)
- 🟡 VectorMath  (ProvostXMemory.VectorIndex)
- 🟡 VectorNamespaceFile  (ProvostXMemory.VectorIndex)
  - VectorNamespaceFile.Content  (ProvostXMemory.VectorIndex) [nested]
  - VectorNamespaceFile.Entry  (ProvostXMemory.VectorIndex) [nested]
- 🟡 VectorScanner  (ProvostXMemory.VectorIndex)
- 🟡 VectorTopKBuffer  (ProvostXMemory.VectorIndex)

## ProvostXMemory.VectorIndex.Engine (13 types, 1244 code LOC excl. comments)

- 🟡 EngineVectorIndexFactory : IVectorIndexFactory  (ProvostXMemory.VectorIndex.Engine)
  - EngineVectorIndexFactory.Options  (ProvostXMemory.VectorIndex.Engine) [nested]
- 🟡 FlatExactIndex : IVectorIndex  (ProvostXMemory.VectorIndex.Engine)
  - FlatExactIndex.Options  (ProvostXMemory.VectorIndex.Engine) [nested]
- 🟡 HnswBeamSearcher  (ProvostXMemory.VectorIndex.Engine)
- 🟡 HnswBuilder  (ProvostXMemory.VectorIndex.Engine)
- 🟡 HnswGraph  (ProvostXMemory.VectorIndex.Engine)
- 🟡 HnswIndex : IVectorIndex  (ProvostXMemory.VectorIndex.Engine)
- 🟡 HnswLevelAssigner  (ProvostXMemory.VectorIndex.Engine)
- 🟣 HnswOptions  (ProvostXMemory.VectorIndex.Engine)  deps: [int, int, double, int, int, int]
- 🟣 HnswPublishedGraph  (ProvostXMemory.VectorIndex.Engine)  deps: [string[], HnswGraph]
- 🟡 HnswSearchContext  (ProvostXMemory.VectorIndex.Engine)
- 🟡 HnswSearcher  (ProvostXMemory.VectorIndex.Engine)
- 🟡 IvfFlatIndex : IVectorIndex  (ProvostXMemory.VectorIndex.Engine)
  - IvfFlatIndex.Options  (ProvostXMemory.VectorIndex.Engine) [nested]
- 🟣 struct  (ProvostXMemory.VectorIndex.Engine)  deps: [int, float]

## ProvostXMemory.VectorIndex.Gpu (15 types, 1523 code LOC excl. comments)

- 🟡 DefaultGpuContextPolicy : IGpuContextPolicy  (ProvostXMemory.VectorIndex.Gpu)
- 🟡 GpuAccelerator : IGpuAccelerator  (ProvostXMemory.VectorIndex.Gpu)
- 🟡 GpuDiscoveryOptions  (ProvostXMemory.VectorIndex.Gpu)
- 🟠 GpuDistanceMetric  (ProvostXMemory.VectorIndex.Gpu)
- 🟡 GpuFlatIndex : IVectorIndex  (ProvostXMemory.VectorIndex.Gpu)
  - GpuFlatIndex.Options  (ProvostXMemory.VectorIndex.Gpu) [nested]
- 🟡 GpuIvfIndex : IVectorIndex  (ProvostXMemory.VectorIndex.Gpu)
  - GpuIvfIndex.Options  (ProvostXMemory.VectorIndex.Gpu) [nested]
- 🟡 GpuLaunchOptions  (ProvostXMemory.VectorIndex.Gpu)
- 🟡 GpuMetricCodec  (ProvostXMemory.VectorIndex.Gpu)
- 🟡 GpuVectorIndexFactory : IVectorIndexFactory  (ProvostXMemory.VectorIndex.Gpu)
- 🟡 GpuVectorKernels  (ProvostXMemory.VectorIndex.Gpu)
- 🟡 GpuVectorMatrix : IDisposable  (ProvostXMemory.VectorIndex.Gpu)
- 🔵 IGpuAccelerator : IDisposable  (ProvostXMemory.VectorIndex.Gpu)
- 🔵 IGpuContextPolicy  (ProvostXMemory.VectorIndex.Gpu)
- 🔵 IGpuScoreLauncher  (ProvostXMemory.VectorIndex.Gpu)
- 🟡 IlgpuScoreLauncher : IGpuScoreLauncher  (ProvostXMemory.VectorIndex.Gpu)

## ProvostXMemory.VectorIndex.GpuTests (6 types, 633 code LOC excl. comments)

- 🟡 GpuAvailabilityAndDeterminismTests  (ProvostXMemory.GpuVectorIndexTests)
- 🟡 GpuFlatIndexConformanceTests : VectorIndexConformanceTests  (ProvostXMemory.VectorIndex.GpuTests)
- 🟡 GpuFlatIndexDeterminismTests  (ProvostXMemory.VectorIndex.GpuTests)
- 🟡 GpuIvfIndexConformanceTests : GpuVectorIndexConformanceTests  (ProvostXMemory.GpuVectorIndexTests)
- 🟡 GpuIvfRecallTests  (ProvostXMemory.GpuVectorIndexTests)
- 🟡 GpuVectorIndexConformanceTests  (ProvostXMemory.GpuVectorIndexTests)

## ProvostXMemory.VectorIndexTests (13 types, 978 code LOC excl. comments)

- 🟡 FlatExactIndexConformanceTests : VectorIndexConformanceTests  (ProvostXMemory.VectorIndexTests)
- 🟡 FlatJsonVectorIndexConformanceTests : VectorIndexConformanceTests  (ProvostXMemory.VectorIndexTests)
- 🟡 FlatJsonVectorIndexTests  (ProvostXMemory.VectorIndexTests)
- 🟡 HnswFactoryTests  (ProvostXMemory.VectorIndexTests)
- 🟡 HnswIndexConformanceTests : VectorIndexConformanceTests  (ProvostXMemory.VectorIndexTests)
- 🟡 HnswIndexTests  (ProvostXMemory.VectorIndexTests)
- 🟡 IvfFlatIndexConformanceTests : VectorIndexConformanceTests  (ProvostXMemory.VectorIndexTests)
- 🟡 IvfFlatIndexTests  (ProvostXMemory.VectorIndexTests)
- 🟡 VectorIndexConformanceTests  (ProvostXMemory.VectorIndexTests)
- 🟡 VectorIndexFactoryTests  (ProvostXMemory.VectorIndexTests)
- 🟡 VectorItemStoreTests  (ProvostXMemory.VectorIndexTests)
- 🟡 VectorMathTests  (ProvostXMemory.VectorIndexTests)
- 🟡 VectorTopKBufferTests  (ProvostXMemory.VectorIndexTests)

## ProvostXMemoryTests (6 types, 766 code LOC excl. comments)

- 🟡 FacadeRecordingLogSink : IMemoryLogSink  (ProvostXMemoryTests)
- 🟡 FacadeStore : IMemoryStore, ProvostXMemory.IScopesAware  (ProvostXMemoryTests)
- 🟡 GraphIndexLedgerClusterTests : IDisposable  (ProvostXMemoryTests)
- 🟡 HashEmbedder : IVectorEmbedder  (ProvostXMemoryTests)
- 🟡 MemoryServiceLoggingTests : IDisposable  (ProvostXMemoryTests)
- 🟡 MemoryServiceTests : IDisposable  (ProvostXMemoryTests)

