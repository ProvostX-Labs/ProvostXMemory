# ProvostXMemory.RetrievalTests.API.md

Types: 7  |  LOC: 748  |  ~439 tokens

---

Dependencies: ProvostXMemory.Abstractions, ProvostXMemory.Fusion, ProvostXMemory.Lexical, ProvostXMemory.Retrieval, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex

### Class: HashEmbedder
> Deterministic hash-based fake embedder (independent of the Consolidation test double):
Implements: IVectorEmbedder
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Lexical, ProvostXMemory.VectorIndex

### Class: HybridRetrieverTests
> HybridRetriever: parallel dense+lexical fan-in, RRF fusion, per-scope isolation, diagnostics.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Fusion, ProvostXMemory.Retrieval, ProvostXMemory.Telemetry, Xunit

### Class: InMemoryStore
> Deterministic hash-based fake embedder (independent of the Consolidation test double):
Implements: IMemoryStore
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Lexical, ProvostXMemory.VectorIndex

### Class: MultiRepresentationRetrieverTests
> MultiRepresentationRetriever: per-modality representation hits merge to parent records.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Retrieval, ProvostXMemory.VectorIndex, Xunit

### Class: QueryPlanRunnerTests
> QueryPlanRunner: fan-out across target scopes, merge, weak-evidence second pass.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Fusion, ProvostXMemory.Retrieval, Xunit

### Class: ScopeIndexRegistryTests
> InMemoryScopeIndexRegistry: scope-exact registration, lookup, miss behavior.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Lexical, ProvostXMemory.Retrieval, ProvostXMemory.VectorIndex, Xunit

### Class: ScopeIndexRig
> Deterministic hash-based fake embedder (independent of the Consolidation test double):
Constructor:
  - ScopeIndexRig(InMemoryStore store, MemoryScope scope, IVectorEmbedder embedder)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Lexical, ProvostXMemory.VectorIndex
