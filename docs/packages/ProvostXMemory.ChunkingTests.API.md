# ProvostXMemory.ChunkingTests.API.md

Types: 5  |  LOC: 650  |  ~238 tokens

---

Dependencies: ProvostXMemory.Abstractions, ProvostXMemory.Chunking, ProvostXMemory.Storage

### Class: ChunkIdCodecTests
> Canonical chunk-id codec composition/parsing: scope-bound ids, malformed input
Cross-package deps: ProvostXMemory.Chunking, Xunit

### Class: ChunkParentResolverTests
> ChunkParentResolver: malformed ids, wrong/foreign scopes, real store round-trip.
Implements: IDisposable
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Chunking, ProvostXMemory.Storage, Xunit

### Class: ScaffoldPlaceholderTests
> Placeholder: proves the test project wires up. Replaced by real suites per type.
Cross-package deps: Xunit

### Class: SizeChunkerTests
> SizeChunker: separator-aware splitting, lossless offsets, overlap sharing, clamps.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Chunking, Xunit

### Class: StructuredChunkerTests
> StructuredChunker: headings, paragraphs, sentence fallback, min/max clamps, offsets, ids.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Chunking, Xunit
