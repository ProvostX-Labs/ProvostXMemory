# Backends

Every vector index backend implements the same `IVectorIndex` contract and must pass the same conformance suite (build, add, remove, search, flush, dimension/count invariants). Switching backends is a configuration change: set `VectorOptions.IndexKind` and wire the matching `IVectorIndexFactory`.

## The contract

| Member | Description |
|---|---|
| `Build(items)` | Replaces index contents with the given `VectorItem` set. |
| `Add(item)` / `Remove(id)` | Incremental mutation (where supported). |
| `Search(vector, k)` | Returns `ScoredVectorId` list, best first. |
| `Dim` / `Count` | Dimension and current item count. |
| `Flush` | Persists durable state. |

Guards (`IVectorIndexGuard`) protect against dimension and model-hash mismatches; violations throw `VectorIndexGuardMismatchException`. Missing ids throw `VectorIndexIdNotFoundException`.

## FlatJson (default, core package)

- **Mechanism:** vectors held in a per-scope `VectorNamespaceFile` (transactional JSON file, written via `AtomicFileWriter`); search is brute-force cosine over the scope's items.
- **Complexity:** build O(n·d); search O(n·d) per query.
- **When to choose:** small scopes (up to a few thousand vectors), persistence-first deployments, environments where zero extra dependencies matter. It is also the parity baseline all other backends are tested against.
- **Persistence:** JSON in the scope's `vectors/<model-hash>/` directory; survives restarts without a rebuild.

## FlatExact (own engine, SIMD)

- **Package:** `ProvostXMemory.VectorIndex.Engine`.
- **Mechanism:** exact brute-force search with `Vector512` SIMD kernels (`VectorMath`, `VectorScanner`, `VectorTopKBuffer`); in-memory item store with configurable distance metric (`Engine Options`).
- **Complexity:** build O(n·d); search O(n·d) but with a large SIMD constant-factor win over FlatJson.
- **When to choose:** exactness required and up to ~10k vectors per scope; also the reference result source for cross-backend parity tests.

## IVF (own engine)

- **Package:** `ProvostXMemory.VectorIndex.Engine`.
- **Mechanism:** k-means coarse quantizer (`KMeansQuantizer`, seeded and deterministic) partitions vectors into inverted posting lists; a query scans only the nearest `nprobe` lists. `IvfPostingLists`, `IvfMember`, `KMeansResult` are the supporting types.
- **Complexity:** build O(iterations · n · d); search O(nprobe/ncentroids · n · d) — sublinear in n.
- **Trade-off:** recall depends on `nprobe`; results are deterministic for a given seed and build order.
- **When to choose:** 10k–1M vectors where a small, tunable recall loss buys large search-speed gains.

## HNSW (own engine)

- **Package:** `ProvostXMemory.VectorIndex.Engine`.
- **Mechanism:** hierarchical navigable small-world graph — `HnswBuilder` constructs layered graphs (`HnswGraph`), level assignment is seeded (`HnswLevelAssigner`), search is greedy beam descent (`HnswSearcher`, `HnswBeamSearcher`).
- **Complexity:** build O(n·log n·d); search approximately O(log n·d).
- **Patent-safe semantics:** indexes are **immutable after build**; mutations trigger a batch rebuild. This deliberately avoids incremental dynamic-update methods (see the patent addendum in ARCHITECTURE.md).
- **Quality:** measured recall@10 = 0.998 on a 2000×64 synthetic corpus; two identical builds produce bit-identical results (determinism pinned by tests).
- **When to choose:** 10k–10M vectors with high recall requirements and read-mostly workloads.

## GPU (ILGPU)

- **Package:** `ProvostXMemory.VectorIndex.Gpu`.
- **Mechanism:** own ILGPU kernels for exact flat scoring and IVF list scans (`GpuVectorKernels`, `IlgpuScoreLauncher`, `GpuVectorMatrix`). Kernels are one-thread-per-slot with chunked launches — no barriers or shared memory — so accumulation order is identical to CPU: results are **bit-exact** between GPU and fallback paths.
- **Availability:** `IGpuAccelerator` discovery is lazy and exception-guarded (`IGpuContextPolicy`, `GpuDiscoveryOptions`). On machines without a compatible accelerator the index transparently delegates to an injected CPU `IVectorIndex` with identical results; the same conformance suite passes on CPU-only machines via this path.
- **Factories:** `GpuVectorIndexFactory` wraps an `EngineVectorIndexFactory` for the fallback; the engine factory itself rejects `IndexKind.Gpu` by design (explicit routing, pinned by tests).
- **When to choose:** Flat/IVF-sized workloads where an accelerator is present and batch scoring throughput matters.

## Selection matrix

| Backend | Exact? | Scale guidance | Extra package | Native deps |
|---|---|---|---|---|
| FlatJson | yes | ≤ a few thousand per scope | none | none |
| FlatExact | yes | ≤ ~10k per scope | Engine | none |
| IVF | approximate (tunable) | 10k–1M | Engine | none |
| HNSW | approximate | 10k–10M | Engine | none |
| GPU (Flat/IVF) | exact (bit-identical) | Flat/IVF scale, accelerator-dependent | Gpu | GPU runtime via ILGPU |

Rules of thumb: start with FlatJson; move to HNSW when a scope outgrows brute-force latency; use IVF when you can trade a little recall for speed; configure GPU unconditionally when hardware may be present — the fallback makes it safe to configure unconditionally.
