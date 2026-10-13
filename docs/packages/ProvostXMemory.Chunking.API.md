# ProvostXMemory.Chunking.API.md

Types: 7  |  LOC: 510  |  ~414 tokens

---

Dependencies: ProvostXMemory.Abstractions

### Interface: IChunker
> Splits memory records into chunks per options (structural/size/overlap).
Methods:
  - IReadOnlyList<Chunk> Split(MemoryRecord record, ChunkOptions options)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IParentResolver
> Splits memory records into chunks per options (structural/size/overlap).
Methods:
  - Task<MemoryRecord?> ResolveAsync(string chunkId, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Class: ChunkIdCodec
> Canonical chunk-id codec: "<c>scopeKind|scopeId|recordId|chunkIndex</c>" (ordinal
Cross-package deps: ProvostXMemory.Abstractions

### Class: ChunkParentResolver
> Resolves a canonical chunk id to its parent record via an <see cref="IMemoryStore"/>
Implements: IParentResolver
Constructor:
  - ChunkParentResolver(IMemoryStore store, ChunkIdCodec? codec = null)
Cross-package deps: ProvostXMemory.Abstractions

### Class: SizeChunker
> Pure size + overlap splitting for unstructured content. Two modes:
Implements: IChunker
Constructor:
  - SizeChunker(ChunkIdCodec? codec = null)
Cross-package deps: ProvostXMemory.Abstractions

### Class: StructuredChunker
> Splits record content by markdown structure: headings become their own chunks, each
Implements: IChunker
Constructor:
  - StructuredChunker(ChunkIdCodec? codec = null)
Cross-package deps: ProvostXMemory.Abstractions

### Record: ChunkIdParts
> Canonical chunk-id codec: "<c>scopeKind|scopeId|recordId|chunkIndex</c>" (ordinal
Constructor:
  - ChunkIdParts(MemoryScopeKind ScopeKind, string ScopeId, string RecordId, int ChunkIndex)
Cross-package deps: ProvostXMemory.Abstractions
