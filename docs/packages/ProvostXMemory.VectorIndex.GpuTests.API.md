# ProvostXMemory.VectorIndex.GpuTests.API.md

Types: 6  |  LOC: 633  |  ~396 tokens

---

Dependencies: ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, ProvostXMemory.VectorIndex.Gpu, ProvostXMemory.VectorIndexTests

### Class: GpuAvailabilityAndDeterminismTests
> Package-level safety tests: no-GPU hand-shake behavior, lazy kernel loading, and
Cross-package deps: ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, ProvostXMemory.VectorIndex.Gpu, ILGPU.Runtime.Cuda, ILGPU.Runtime.OpenCL, Xunit

### Class: GpuFlatIndexConformanceTests
> Conformance fixture for the ILGPU GPU backend. On this machine no discrete GPU is
Implements: VectorIndexConformanceTests
Cross-package deps: ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, ProvostXMemory.VectorIndex.Gpu, ProvostXMemory.VectorIndexTests, Xunit

### Class: GpuFlatIndexDeterminismTests
> Determinism contract: whatever path GpuFlatIndex takes (ILGPU kernel here — this
Cross-package deps: ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, ProvostXMemory.VectorIndex.Gpu, ProvostXMemory.VectorIndexTests, Xunit

### Class: GpuIvfIndexConformanceTests
> Conformance fixture for GpuIvfIndex. Exact-ranking tests are replaced by the
Implements: GpuVectorIndexConformanceTests
Cross-package deps: ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, ProvostXMemory.VectorIndex.Gpu, Xunit

### Class: GpuIvfRecallTests
> Recall gate for GpuIvfIndex on clustered fixtures — identical methodology to the
Cross-package deps: ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, ProvostXMemory.VectorIndex.Gpu, Xunit

### Class: GpuVectorIndexConformanceTests
> Shared Gpu-package conformance suite: every Gpu index is exercised through the same
Cross-package deps: ProvostXMemory.VectorIndex, Xunit
