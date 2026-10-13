# FAQ

## How does scope isolation actually work?

Isolation is structural. Every record, index, and graph artifact lives under its scope's resolved directory path (`<root>/<scope-kind>/<scope-id>/…`), every API operation is scope-bound, and query routing resolves indexes per scope through a scope-exact registry. A query in one scope has no code path that can read another scope's data. Cross-scope access happens only when you explicitly declare it via a `QueryPlan` fan-out. This is enforced at the storage, retrieval, and end-to-end levels by dedicated tests.

## Can I swap the embedding model?

Yes, but it is a migration, not a toggle. Vectors are meaningless across models, so index artifacts are namespaced by model hash and guarded at open time (`VectorIndexGuardMismatchException`). To switch: configure a new model hash and dimension in `VectorOptions` and `MemoryRootLayout`, then re-embed and `RebuildAsync()`. Old artifacts under the previous hash remain on disk until you remove them. The library never downloads or selects models — the embedder is your injected `IVectorEmbedder`.

## Does it need a server?

No. ProvostXMemory is an in-process library. Storage is transactional JSON/JSONL files under a root directory; the vector engine is pure C#. The only external integration points are the ones you inject (embedding model, optional grader/rewriter/reranker clients, optional external fetcher).

## What are the GPU requirements?

None mandatory. The GPU layer (`ProvostXMemory.VectorIndex.Gpu`, shipped **in the single `ProvostXMemory` package**) uses ILGPU kernels for Flat and IVF search, with lazy, exception-guarded accelerator discovery. On CPU-only machines — or with no compatible accelerator — the index transparently falls back to an injected CPU implementation with **bit-identical** results (identical accumulation order by construction). Configuring GPU unconditionally is therefore safe; the same conformance suite passes on CPU-only machines via the fallback.

## What is the persistence format?

Human-readable files: JSONL for records (`records.jsonl`), JSON for manifests and FlatJson vector namespaces, JSONL for graph entities/relations/clusters. All writes go through `AtomicFileWriter` (temp file + atomic swap, optional fsync). Engine indexes persist their own artifact formats under `vectors/<model-hash>/`. Old-format record files are read forward-compatibly (Reps/Meta additions are backward-compatible).

## Is it thread-safe?

Public facade operations are asynchronous and safe to call concurrently. Index mutation follows a build-or-rebuild model (HNSW indexes are immutable after build; mutations trigger batch rebuilds), which avoids the classic concurrent-mutation hazards. For heavy concurrent write workloads, serialize writes per scope as you would any transactional file store. Conformance tests exercise the documented behaviors; treat per-index instances as not thread-safe for concurrent mutation.

## How do I make recall results appear?

Call `BuildIndexesAsync(scope)` after remembering records — index state is intentionally cold until built, and a fresh service over an existing root restores indexes via `BuildIndexesAsync`/`RebuildAsync`. Rankings after a rebuild are identical to before (determinism is tested).

## Which backend should I pick?

Start with the zero-dependency FlatJson default. Move to the engine's FlatExact for SIMD-speed exact search, IVF at 10k–1M vectors when you accept a tunable recall trade-off, and HNSW at 10k–10M vectors for high-recall approximate search (measured recall@10 = 0.998). See [Backends](backends.md).

## Can I use my own reranker or grader?

Yes — that is the design. Implement `IReranker` (or `IRerankerClient` for an external rerank service), `IRetrievalGrader`, and `IQueryRewriter`, and inject them. The library is LLM-agnostic: it orchestrates the corrective loop but never owns a model.

## How does consolidation decide what to delete?

Deduplication keeps the **oldest** record of a near-duplicate cluster and unions the tags onto the survivor; TTL expiry deletes records past `ExpiresAt`; cluster summaries are additive (new `summary`-category records plus graph clusters). All decisions are deterministic for a given store state and options. See [Consolidation](consolidation.md).

## Does logging leak record content?

No. `IncludeContent` defaults to `false`, and when disabled, record content never appears in any log output — enforced by content-leak tests. Log entries carry scope identity, category, timings, and correlation ids, not memory content.

## How do I verify logs have not been tampered with?

Enable `MemoryLogIntegrity.HashChain` and periodically run `LogChainVerifier.Verify(path)`. Each entry's hash covers its sequence number, timestamp, category, payload, and the previous entry's hash, so edits, deletions, and reordering are all detectable. See [Logging](logging.md#the-audit-hash-chain).

## Can one record id exist in multiple scopes?

Yes — ids are unique per scope, not globally, and query plans handle collisions via scope-qualified fusion identifiers so identical ids never conflate across scopes.

## What .NET version is required?

.NET 8. There are no native library requirements beyond the .NET runtime; the GPU package adds the ILGPU runtime only if you reference it.
