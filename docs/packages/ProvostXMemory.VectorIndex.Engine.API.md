# ProvostXMemory.VectorIndex.Engine.API.md

Types: 23  |  LOC: 1871  |  ~1083 tokens

---

Dependencies: ProvostXMemory.Abstractions

### Class: EngineVectorIndexFactory
> Own-engine factory: Flat → FlatExactIndex, Ivf → IvfFlatIndex, Hnsw → HnswIndex.
Implements: IVectorIndexFactory
Constructor:
  - EngineVectorIndexFactory(Options options)
Cross-package deps: ProvostXMemory.Abstractions

### Class: FlatExactIndex
> Own-engine exact backend: SIMD brute-force scan (Vector512→256→128, scalar fallback)
Implements: IVectorIndex
Constructor:
  - FlatExactIndex(IVectorIndexGuard guard, IVectorIndexGuard guard, Options options)

### Class: HnswBeamSearcher
> Exact analog of the canonical HNSW SEARCH-LAYER routine, in score form (higher = closer):
Constructor:
  - HnswBeamSearcher(HnswSearchContext context)

### Class: HnswBuilder
> Own HNSW construction (canonical INSERT algorithm, level-0 full beam): inserts each item
Constructor:
  - HnswBuilder(HnswOptions options, VectorScanner scanner)

### Class: HnswGraph
> A single candidate during graph traversal: node id + metric score.
Constructor:
  - HnswGraph(int minLayerCapacity)

### Class: HnswIndex
> Own-engine HNSW backend with IMMUTABLE-INDEX + BATCH-REBUILD semantics (patent-avoidance
Implements: IVectorIndex
Constructor:
  - HnswIndex(IVectorIndexGuard guard, IVectorIndexGuard guard, HnswOptions options)

### Class: HnswLevelAssigner
> Deterministic level assignment for HNSW layers: each node's top level is drawn from the
Constructor:
  - HnswLevelAssigner(double levelFactor, int seed)

### Class: HnswSearchContext
> Reusable traversal context for one built HNSW graph: row-order ids parallel to
Constructor:
  - HnswSearchContext(string[] ids, float[][] normalizedRows)

### Class: HnswSearcher
> Query-time traversal over a built HNSW graph: greedy descent from the entry point
Constructor:
  - HnswSearcher(HnswOptions options, HnswSearchContext context)

### Class: IvfFlatIndex
> Own-engine IVF-Flat: deterministic k-means coarse quantizer + inverted lists;
Implements: IVectorIndex
Constructor:
  - IvfFlatIndex(IVectorIndexGuard guard, IVectorIndexGuard guard, Options options)

### Class: IvfPostingLists
> Inverted lists: cluster id → member rows + centroid access for probing.
Constructor:
  - IvfPostingLists(float[][] centroids, IReadOnlyList<IvfMember> members)

### Class: KMeansQuantizer
> OWN deterministic k-means (Lloyd iterations, seeded) — the IVF coarse quantizer.
Constructor:
  - KMeansQuantizer(int seed)

### Class: KMeansResult
> Result of one k-means run: centroids plus label arrays over the training rows.
Constructor:
  - KMeansResult(float[][] centroids, int[] labels)

### Class: Options
> Own-engine exact backend: SIMD brute-force scan (Vector512→256→128, scalar fallback)
Constructor:
  - Options(VectorDistanceMetric metric)

### Class: Options
> Own-engine IVF-Flat: deterministic k-means coarse quantizer + inverted lists;
Constructor:
  - Options(int nlist, int nprobe, int kmeansIterations, int seed, int nlist, int nprobe, int kmeansIterations, int seed, VectorDistanceMetric metric)

### Class: Options
> Own-engine factory: Flat → FlatExactIndex, Ivf → IvfFlatIndex, Hnsw → HnswIndex.
Constructor:
  - Options(FlatExactIndex.Options flatOptions, IvfFlatIndex.Options ivfOptions, HnswOptions hnswOptions)
Cross-package deps: ProvostXMemory.Abstractions

### Class: SeededRandom
> Pure deterministic pseudo-random generator (xorshift64*) used wherever the engine needs
Constructor:
  - SeededRandom(int seed)

### Class: VectorItemStore
> Mutable in-memory store shared by engine indexes: ids in row order parallel to vector
Constructor:
  - VectorItemStore(int dim)

### Class: VectorMath
> PURE STATIC UTILITY (no mutable state, thread-safe by construction; the one sanctioned

### Class: VectorScanner
> Scans rows with the metric's SIMD kernel; shared scoring core for flat/IVF search.
Constructor:
  - VectorScanner(VectorDistanceMetric metric)

### Class: VectorTopKBuffer
> Bounded top-k min-heap buffer over (id, score); scores rise to the top.
Constructor:
  - VectorTopKBuffer(int k)

### Record: HnswPublishedGraph
> The published immutable snapshot of a built HNSW graph: row-order ids parallel to the
Constructor:
  - HnswPublishedGraph(string[] Ids, HnswGraph Graph, float[][] NormalizedRows)

### Record: IvfMember
> Inverted lists: cluster id → member rows + centroid access for probing.
Constructor:
  - IvfMember(string Id, float[] Vector, int Cluster)
