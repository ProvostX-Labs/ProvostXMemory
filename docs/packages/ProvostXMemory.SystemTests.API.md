# ProvostXMemory.SystemTests.API.md

Types: 18  |  LOC: 1811  |  ~1510 tokens

---

Dependencies: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Chunking, ProvostXMemory.Consolidation, ProvostXMemory.Eval, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Lexical, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, ProvostXMemory.VectorIndex.Gpu

### Class: BackendCompositionTests
> REAL composition: MemoryServiceBuilder-built MemoryService, production JsonlMemoryStore
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: CaptureFactory
> Shared wiring for the agnostic system tests: every service here is composed exactly as
Implements: IVectorIndexFactory
Constructor:
  - CaptureFactory(IVectorIndexFactory inner)
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, ProvostXMemory.Eval, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, ProvostXMemory.VectorIndex.Gpu, Xunit

### Class: ChunkOptionsEdgeTests
> RetrievalQuery/filter behavioral semantics: the query model is an immutable record —
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Chunking, ProvostXMemory.Fusion, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: ConsolidationAndGraphTests
> The consolidation pass end-to-end through the facade: dedup keeps the oldest record
Implements: IDisposable
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, ProvostXMemory.Eval, ProvostXMemory.Graph, ProvostXMemory.Storage, ProvostXMemory.VectorIndex, Xunit

### Class: EvalReportRoundTripTests
> EvalReport JSON round-trip through the module's own serializer: all fields survive,
Cross-package deps: ProvostXMemory.Eval, Xunit

### Class: FlatJsonFactory
> Shared wiring for the agnostic system tests: every service here is composed exactly as
Implements: IVectorIndexFactory
Constructor:
  - FlatJsonFactory(string root, string modelHash)
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, ProvostXMemory.Eval, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, ProvostXMemory.VectorIndex.Gpu, Xunit

### Class: FusionWeightTests
> FusionOptions weight semantics on the fuser used by the facade: RRF rank-1 baselines
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Eval, ProvostXMemory.Fusion, ProvostXMemory.Lexical, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: HnswDeterminismTests
> FusionOptions weight semantics on the fuser used by the facade: RRF rank-1 baselines
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Eval, ProvostXMemory.Fusion, ProvostXMemory.Lexical, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: KnowledgeAssistantJourneyTests
> THE knowledge-assistant journey, as a host would experience it: seed a scope with the
Implements: IDisposable
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, ProvostXMemory.Eval, ProvostXMemory.Fusion, ProvostXMemory.VectorIndex, Xunit

### Class: LifecycleJourneyTests
> THE lifecycle journey on the public IMemoryService surface: remember → recall →
Implements: IDisposable
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Fusion, ProvostXMemory.VectorIndex, Xunit

### Class: MemoryScopeSemanticsTests
> MemoryScope hierarchy semantics at type level: chain flattening, parent containment,
Cross-package deps: ProvostXMemory.Abstractions, Xunit

### Class: MultiTenantJourneyTests
> THE multi-tenant journey: three scopes of different kinds (platform root, session
Implements: IDisposable
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Fusion, ProvostXMemory.VectorIndex, Xunit

### Class: PersistenceAndRoutingTests
> Full restart simulation and options-routing behavior: a new MemoryService instance
Implements: IDisposable
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, ProvostXMemory.Fusion, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: RetrievalFiltersEnforcementTests
> RetrievalFilters enforcement (wave-7 gap closure): tags/category/date filters carried
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: RetrievalQueryFilterTests
> RetrievalQuery/filter behavioral semantics: the query model is an immutable record —
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Chunking, ProvostXMemory.Fusion, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: SystemRig
> Shared wiring for the agnostic system tests: every service here is composed exactly as
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, ProvostXMemory.Eval, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, ProvostXMemory.VectorIndex.Gpu, Xunit

### Class: TempRoot
> Shared wiring for the agnostic system tests: every service here is composed exactly as
Implements: IDisposable
Constructor:
  - TempRoot(string tag)
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, ProvostXMemory.Eval, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, ProvostXMemory.VectorIndex.Gpu, Xunit

### Class: TopicCorpus
> Shared wiring for the agnostic system tests: every service here is composed exactly as
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, ProvostXMemory.Eval, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, ProvostXMemory.VectorIndex.Gpu, Xunit
