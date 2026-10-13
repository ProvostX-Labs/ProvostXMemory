# ProvostXMemory.StorageTests.API.md

Types: 6  |  LOC: 633  |  ~217 tokens

---

Dependencies: ProvostXMemory.Abstractions, ProvostXMemory.Storage

### Class: AtomicFileWriterTests
> Atomic-write behavior: temp cleanup after success and simulated crash debris.
Cross-package deps: Xunit, ProvostXMemory.Storage

### Class: JsonlMemoryStoreTests
Implements: IDisposable
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Storage, Xunit

### Class: MemoryManifestCodecTests
Implements: IDisposable
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Storage, Xunit

### Class: MemoryRootLayoutTests
Cross-package deps: Xunit, ProvostXMemory.Storage

### Class: ScaffoldPlaceholderTests
> Keeps a minimal smoke test while the real suites are in dedicated files.
Cross-package deps: Xunit

### Class: ScopeIsolationTests
> Mandatory per ARCHITECTURE.md: cross-scope access must fail structurally.
Implements: IDisposable
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Storage, Xunit
