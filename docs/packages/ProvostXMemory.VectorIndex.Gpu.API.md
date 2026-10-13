# ProvostXMemory.VectorIndex.Gpu.API.md

Types: 16  |  LOC: 1530  |  ~1381 tokens

---

Dependencies: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine

### Interface: IGpuAccelerator
> Availability handle for the ILGPU runtime: owns the Context and exposes the resolved
Implements: IDisposable
Properties:
  - bool IsAvailable { get; set; }
  - string Name { get; set; }
  - string DeviceDescription { get; set; }
Methods:
  - Accelerator RequireAccelerator()
Cross-package deps: ILGPU, ILGPU.Runtime

### Interface: IGpuContextPolicy
> ILGPU accelerator hand-shake: a GPU-first context (CUDA + OpenCL as configured) with
Methods:
  - bool Accepts(AcceleratorType acceleratorType)
  - Context CreateContext(GpuDiscoveryOptions discovery)
  - Device? SelectDevice(Context context, GpuDiscoveryOptions discovery)
Cross-package deps: ILGPU, ILGPU.Runtime, ILGPU.Runtime.CPU, ILGPU.Runtime.Cuda, ILGPU.Runtime.OpenCL

### Interface: IGpuScoreLauncher
> Compiled-kernel launcher boundary: hides ILGPU's launcher delegates behind an
Methods:
  - void ScoreRows(ArrayView<float> query,
        ArrayView<float> rows,
        ArrayView<float> rowNorms,
        ArrayView<float> scores,
        int dim,
        int metric,
        float queryNorm,
        int rowOffset,
        int rowEnd)
  - void ScoreIndexedRows(ArrayView<float> query,
        ArrayView<float> rows,
        ArrayView<float> rowNorms,
        ArrayView<float> scores,
        ArrayView<int> memberRows,
        int dim,
        int metric,
        float queryNorm,
        int candidateOffset,
        int candidateEnd)
Cross-package deps: ILGPU, ILGPU.Runtime

### Class: DefaultGpuContextPolicy
> Default policy: GPU-first discovery (CUDA/OpenCL per options), optional CPU fallback
Implements: IGpuContextPolicy
Constructor:
  - DefaultGpuContextPolicy(bool allowCpuFallback)
Cross-package deps: ILGPU, ILGPU.Runtime, ILGPU.Runtime.CPU, ILGPU.Runtime.Cuda, ILGPU.Runtime.OpenCL

### Class: GpuAccelerator
> Owns the ILGPU Context and the resolved accelerator. The GPU hand-shake runs once,
Implements: IGpuAccelerator
Constructor:
  - GpuAccelerator(IGpuContextPolicy contextPolicy, GpuDiscoveryOptions discovery, GpuLaunchOptions launchOptions)
Cross-package deps: ILGPU, ILGPU.Runtime

### Class: GpuDiscoveryOptions
> How the ILGPU context is built for accelerator discovery. GPU-first builders enable
Constructor:
  - GpuDiscoveryOptions(bool tryCuda, bool tryOpenCl)
Cross-package deps: ILGPU, ILGPU.Runtime, ILGPU.Runtime.CPU, ILGPU.Runtime.Cuda, ILGPU.Runtime.OpenCL

### Class: GpuFlatIndex
> GPU-accelerated exact flat index (ILGPU kernel scan). When no GPU is available the
Implements: IVectorIndex
Constructor:
  - GpuFlatIndex(IVectorIndexGuard guard, IVectorIndex fallback, IGpuAccelerator gpu, IVectorIndexGuard guard, IVectorIndex fallback, IGpuAccelerator gpu, IVectorIndexGuard guard, IVectorIndex fallback, Options options, IGpuAccelerator gpu, IGpuScoreLauncher launcher, IVectorIndexGuard guard, IVectorIndex fallback, Options options)
Cross-package deps: ILGPU.Runtime, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine

### Class: GpuIvfIndex
> GPU-accelerated IVF index: the deterministic engine k-means partitions the corpus
Implements: IVectorIndex
Constructor:
  - GpuIvfIndex(IGpuAccelerator gpu, IVectorIndexGuard guard, IVectorIndex fallback, IGpuAccelerator gpu, IVectorIndexGuard guard, IVectorIndex fallback, Options options, IGpuAccelerator gpu, IGpuScoreLauncher launcher, IVectorIndexGuard guard, IVectorIndex fallback, Options options)
Cross-package deps: ILGPU.Runtime, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine

### Class: GpuLaunchOptions
> Kernel-launch policy: rows per launch chunk and the per-launch thread cap. Chunked
Constructor:
  - GpuLaunchOptions(int rowsPerLaunch, int maxThreadsPerLaunch)

### Class: GpuMetricCodec
> Mapping from the engine metric enum onto the GPU kernel's int metric code.
Cross-package deps: ProvostXMemory.VectorIndex.Engine

### Class: GpuVectorIndexFactory
> Factory that produces <see cref="GpuFlatIndex"/> for <see cref="IndexKind.Gpu"/> and
Implements: IVectorIndexFactory
Constructor:
  - GpuVectorIndexFactory(IGpuAccelerator gpu, EngineVectorIndexFactory engine)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex.Engine

### Class: GpuVectorKernels
> ILGPU kernels: one thread per score slot, plain loops, no barriers/shared memory —
Cross-package deps: ILGPU, ILGPU.Runtime

### Class: GpuVectorMatrix
> GPU-resident row-major vector matrix with lazy (re)upload: CPU-side rows + norms are
Implements: IDisposable
Constructor:
  - GpuVectorMatrix(IGpuAccelerator gpu)
Cross-package deps: ILGPU, ILGPU.Runtime

### Class: IlgpuScoreLauncher
> Lazy, guarded ILGPU kernel launcher: kernels compile on first launch only (never at
Implements: IGpuScoreLauncher
Constructor:
  - IlgpuScoreLauncher(IGpuAccelerator gpu, GpuLaunchOptions launchOptions)
Cross-package deps: ILGPU, ILGPU.Runtime

### Class: Options
> GPU-accelerated IVF index: the deterministic engine k-means partitions the corpus
Constructor:
  - Options(int nlist, int nprobe, int kmeansIterations, int seed, int nlist, int nprobe, int kmeansIterations, int seed, VectorDistanceMetric metric, GpuLaunchOptions launch)
Cross-package deps: ILGPU.Runtime, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine

### Class: Options
> GPU-accelerated exact flat index (ILGPU kernel scan). When no GPU is available the
Constructor:
  - Options(VectorDistanceMetric metric, VectorDistanceMetric metric, GpuLaunchOptions launch)
Cross-package deps: ILGPU.Runtime, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine
