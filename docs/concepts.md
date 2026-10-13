# Concepts

The memory model of ProvostXMemory: what a record is, how scopes isolate data, how representations and chunking work, and how the model-hash guard protects index consistency.

## Records

A `MemoryRecord` is the atomic unit of memory. It is an immutable record type:

| Member | Description |
|---|---|
| `Id` | Stable identifier; unique within its scope. |
| `Scope` | The `MemoryScope` this record belongs to (isolation boundary). |
| `Key` | Short symbolic name (also boosted in lexical scoring). |
| `Content` | Primary text content. |
| `Category` | Optional single-value classification, filterable. |
| `Tags` | Optional label set, filterable; stored normalized (empty list, never null). |
| `Reps` | Additional `MemoryRepresentation` entries (multi-modal). |
| `CreatedAt` / `UpdatedAt` | UTC timestamps, set on creation and maintained by the facade. |
| `ExpiresAt` | Optional TTL deadline; honored by consolidation. |
| `Meta` | Free-form string key/value metadata, persisted with the record. |

Hosts create records; the facade owns index maintenance. `RememberAsync`, `UpdateAsync`, and `ForgetAsync` keep the dense and lexical indexes (and optionally the graph ledger) in sync through a per-record id ledger.

## Scopes and the isolation model

A `MemoryScope` is `(Kind, Id, ParentScope?)` — hierarchical and immutable:

- Kinds: `Platform`, `Session`, `Tenant`, `Purpose`, `Custom`.
- `Chain()` flattens the parent chain, outermost first.
- `Contains(other)` reports whether `other` is this scope or a descendant.

Isolation is **structural, not conventional**: every store, index, and graph operation is scope-bound, and scope resolution is enforced by `MemoryRootLayout`, which maps each scope to its own directory subtree. Cross-scope reads are impossible by construction — a query in scope A cannot observe records, vectors, or graph entities of scope B. This property is pinned by dedicated isolation tests in every layer and by end-to-end multi-tenant journey tests.

Parent chains are host-side metadata: they express intent (a session belonging to a tenant) and participate in `Contains` semantics, but persistence equality is by kind + id — scopes are stored under their own resolved paths regardless of parentage.

## Representations

One record can carry multiple `MemoryRepresentation` entries with modality `Text | Image | Table | Audio | Custom` and an optional per-representation `EmbeddingModelHash`. This supports multi-modal memory: a table and its textual description can live on the same record and be embedded separately.

Retrieval addresses representations with vector ids of the form `repr|<recordId>|<index>`. The multi-representation retriever resolves these back to the owning record and reports the **best-representation score** per record, so one hit per record per stage regardless of how many representations matched.

## Chunking

Long content is split into `Chunk` items before embedding. `IChunker` implementations:

- `StructuredChunker` — respects document structure (line/paragraph boundaries) when `ChunkOptions.RespectStructure` is set.
- `SizeChunker` — size/overlap-driven splitting.

`ChunkOptions` controls `MaxChunkLength` (default 512), `Overlap`, `MinChunkLength`, `Separator`, and structure respect; all combinations are validated (overlap must be smaller than the max length, minimum must not exceed the maximum, and so on). Chunk ids are encoded by `ChunkIdCodec` as scope-qualified identifiers and resolved back to their parent record by `IParentResolver` / `ChunkParentResolver` — the parent-document recall pattern: retrieve the best chunk, return the whole record.

## Model-hash guard

Embedding models define the geometry of their vectors. Mixing vectors produced by different models (or dimensions) in one index silently destroys retrieval quality. ProvostXMemory prevents this at three levels:

1. **Path isolation** — index artifacts live under `vectors/<model-hash>/` inside each scope, so a different model means a different artifact set.
2. **Options guard** — `VectorOptions` carries the model hash and dimension; index files record them at build time.
3. **Runtime guard** — `IVectorIndexGuard` implementations reject opens with a mismatched model hash or dimension, throwing `VectorIndexGuardMismatchException` instead of returning garbage.

Consequence: **swapping the embedding model is a data-migration decision, not a config flag.** Re-embed and rebuild (`RebuildAsync`) under the new hash. See the [FAQ](faq.md#can-i-swap-the-embedding-model).

## Namespaces

The term *namespace* in this codebase means the scope-qualified partition that owns an artifact: a (scope, model-hash) pair for vectors, a scope for records/graph/lexical state. Query plans fan out across namespaces by targeting scope ids; fusion identifiers are scope-qualified so the same record id in two scopes never conflates (see [Retrieval](retrieval.md#query-plans)).
