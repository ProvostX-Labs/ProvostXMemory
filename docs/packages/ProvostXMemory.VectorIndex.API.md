# ProvostXMemory.VectorIndex.API.md

Types: 13  |  LOC: 472  |  ~771 tokens

---

Dependencies: ProvostXMemory.Abstractions

### Interface: IVectorEmbedder
> Host-injected text embedder (library validates hashes/dims, never picks models).
Properties:
  - string ModelHash { get; set; }
  - int Dim { get; set; }
Methods:
  - Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)

### Interface: IVectorIndex
> Pluggable vector index: build/add/remove/search, dim + count, flush.
Properties:
  - int Dim { get; set; }
  - long Count { get; set; }
Methods:
  - Task BuildAsync(IReadOnlyList<VectorItem> items, CancellationToken cancellationToken = default)
  - Task AddAsync(VectorItem item, CancellationToken cancellationToken = default)
  - Task RemoveAsync(string id, CancellationToken cancellationToken = default)
  - Task<IReadOnlyList<ScoredVectorId>> SearchAsync(IReadOnlyList<float> vector, int k, CancellationToken cancellationToken = default)
  - Task FlushAsync(CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IVectorIndexFactory
> Creates Flat/Ivf/Hnsw indexes (own engine or wrapped backends).
Methods:
  - IVectorIndex Create(IndexKind kind, int dim)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IVectorIndexGuard
> Guards model hash + vector dimension at every index mutation boundary.
Properties:
  - string ModelHash { get; set; }
  - int Dim { get; set; }
Methods:
  - void Ensure(string candidateModelHash, int candidateDim)

### Class: Content
> On-disk JSON vector index file format (one file per scope/model-hash namespace):
Constructor:
  - Content(VectorIndexGuard guard, IReadOnlyList<Entry> entries)

### Class: Entry
> On-disk JSON vector index file format (one file per scope/model-hash namespace):
Constructor:
  - Entry(string id, IReadOnlyList<float> vector)

### Class: FlatJsonVectorIndex
> Default zero-dependency backend over the JSON namespace-file format:
Implements: IVectorIndex
Constructor:
  - FlatJsonVectorIndex(VectorNamespaceFile file, IVectorIndexGuard guard)

### Class: ScalarVectorOps
> In-package scalar math for the FlatJson backend (instance-based, no static state).

### Class: VectorIndexGuardMismatchException
> Embedder model hash or vector dimension does not match the index guard.
Implements: Exception
Constructor:
  - VectorIndexGuardMismatchException(string message)

### Class: VectorIndexIdNotFoundException
> Remove of an id that is not present in the index.
Implements: Exception
Constructor:
  - VectorIndexIdNotFoundException(string id)

### Class: VectorNamespaceFile
> On-disk JSON vector index file format (one file per scope/model-hash namespace):
Constructor:
  - VectorNamespaceFile(string path, string modelHash, int dim)

### Record: ScoredVectorId
> Pluggable vector index: build/add/remove/search, dim + count, flush.
Constructor:
  - ScoredVectorId(string Id, float Score)
Cross-package deps: ProvostXMemory.Abstractions

### Record: VectorItem
> Pluggable vector index: build/add/remove/search, dim + count, flush.
Constructor:
  - VectorItem(string Id, IReadOnlyList<float> Vector)
Cross-package deps: ProvostXMemory.Abstractions
