# ProvostXMemory.VectorIndexTests.API.md

Types: 13  |  LOC: 978  |  ~708 tokens

---

Dependencies: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine

### Class: FlatExactIndexConformanceTests
> Conformance fixture for the own-engine SIMD Flat exact backend.
Implements: VectorIndexConformanceTests
Cross-package deps: ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: FlatJsonVectorIndexConformanceTests
> Conformance fixture for the FlatJson backend (JSON namespace file + cosine).
Implements: VectorIndexConformanceTests
Cross-package deps: ProvostXMemory.VectorIndex, Xunit

### Class: FlatJsonVectorIndexTests
> FlatJson backend specifics: persistence round-trip, guards, atomic swap.
Cross-package deps: ProvostXMemory.VectorIndex, Xunit

### Class: HnswFactoryTests
> Engine backend specifics: HNSW determinism, mutation/rebuild lifecycle, factory wiring.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: HnswIndexConformanceTests
> Conformance fixture for the own-engine HNSW backend (immutable-index + batch-rebuild
Implements: VectorIndexConformanceTests
Cross-package deps: ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: HnswIndexTests
> Engine backend specifics: HNSW determinism, mutation/rebuild lifecycle, factory wiring.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: IvfFlatIndexConformanceTests
> Conformance fixture for the own-engine IVF-Flat backend. Exact-ranking search tests are
Implements: VectorIndexConformanceTests
Cross-package deps: ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: IvfFlatIndexTests
> Engine backend specifics: IVF determinism/recall, engine factory wiring.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: VectorIndexConformanceTests
> Shared IVectorIndex conformance suite. Every backend (FlatJson, FlatExact, IvfFlat)
Cross-package deps: ProvostXMemory.VectorIndex, Xunit

### Class: VectorIndexFactoryTests
> Engine backend specifics: IVF determinism/recall, engine factory wiring.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: VectorItemStoreTests
> VectorMath kernels: scalar ground truth vs SIMD paths, normalization, cosine.
Cross-package deps: ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: VectorMathTests
> VectorMath kernels: scalar ground truth vs SIMD paths, normalization, cosine.
Cross-package deps: ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit

### Class: VectorTopKBufferTests
> VectorMath kernels: scalar ground truth vs SIMD paths, normalization, cosine.
Cross-package deps: ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, Xunit
