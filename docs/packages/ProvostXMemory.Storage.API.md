# ProvostXMemory.Storage.API.md

Types: 4  |  LOC: 541  |  ~228 tokens

---

Dependencies: ProvostXMemory.Abstractions

### Class: AtomicFileWriter
> Durability primitive for the storage layer: writes payload to a temporary
Constructor:
  - AtomicFileWriter(bool fsync = false)

### Class: JsonlMemoryStore
> Default JSONL-backed store: one append/rewrite-friendly <c>records.jsonl</c> per
Implements: IJsonlMemoryStore
Constructor:
  - JsonlMemoryStore(MemoryRootLayout layout, AtomicFileWriter? writer = null)
Cross-package deps: ProvostXMemory.Abstractions

### Class: MemoryManifestCodec
> Manifest read/write for a memory root (JSON, atomic swap); missing file reads as empty.
Implements: IMemoryManifestCodec
Constructor:
  - MemoryManifestCodec(AtomicFileWriter? writer = null)
Cross-package deps: ProvostXMemory.Abstractions

### Class: MemoryRootLayout
> Root folder + scope path resolution: <c>&lt;root&gt;/&lt;scope-kind&gt;/&lt;scope-id&gt;/...</c>.
Constructor:
  - MemoryRootLayout(string rootPath, string defaultModelHash)
