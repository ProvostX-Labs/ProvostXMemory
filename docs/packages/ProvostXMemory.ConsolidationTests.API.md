# ProvostXMemory.ConsolidationTests.API.md

Types: 7  |  LOC: 559  |  ~341 tokens

---

Dependencies: ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, ProvostXMemory.VectorIndex

### Class: ClusterSummarizerTests
> ClusterSummarizer: deterministic summary shape, meta flags, cluster id stability.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, Xunit

### Class: ConsolidationReportTests
> ConsolidationReportMeta: report attached to / read from returned records.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, Xunit

### Class: HashFakeEmbedder
> Deterministic hash-based fake embedder: each token hashes into a fixed 512-dim
Implements: IVectorEmbedder
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex

### Class: InMemoryMemoryStore
> In-memory <see cref="IMemoryStore"/> test double. Scope-isolated dictionaries; no
Implements: IMemoryStore
Cross-package deps: ProvostXMemory.Abstractions

### Class: MemoryConsolidatorTests
> MemoryConsolidator end-to-end over in-memory store + fake embedder.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, Xunit

### Class: TestRecords
> Shared record factory for consolidation tests.
Cross-package deps: ProvostXMemory.Abstractions

### Class: ThresholdDedupDetectorTests
> ThresholdDedupDetector: cosine via fake embedder, Jaccard fallback, deterministic pair order.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, ProvostXMemory.ConsolidationTests, Xunit
