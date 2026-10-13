# ProvostXMemoryTests.API.md

Types: 6  |  LOC: 703  |  ~425 tokens

---

Dependencies: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, ProvostXMemory.Correction, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Logging, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine

### Class: FacadeRecordingLogSink
> Test double that records written log entries (thread-safe snapshot).
Implements: IMemoryLogSink
Cross-package deps: ProvostXMemory.Logging

### Class: FacadeStore
> Scope-isolated in-memory <see cref="IMemoryStore"/> for facade tests, plus
Implements: IMemoryStore, IScopesAware
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex, Xunit

### Class: GraphIndexLedgerClusterTests
> GraphIndexLedger cluster recording: IClusterSummarizer-shaped summaries are recorded as
Implements: IDisposable
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, ProvostXMemory.Graph, ProvostXMemory.Storage, Xunit

### Class: HashEmbedder
> Scope-isolated in-memory <see cref="IMemoryStore"/> for facade tests, plus
Implements: IVectorEmbedder
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex, Xunit

### Class: MemoryServiceLoggingTests
> Facade logging wiring: disabled logging creates no log file and costs nothing;
Implements: IDisposable
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Logging, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: MemoryServiceTests
> MemoryService end-to-end: Remember → Recall across scopes with isolation, Forget
Implements: IDisposable
Cross-package deps: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, ProvostXMemory.Correction, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex.Engine, Xunit
