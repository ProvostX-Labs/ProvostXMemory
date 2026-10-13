# ProvostXMemory

A strict-OOP, host-agnostic long-term memory library for .NET — scoped storage, hybrid (dense + lexical) retrieval with enforced filters, consolidation, graph, corrective recall, an eval harness, and audit-grade integrity-logged observability — over pluggable vector-index backends with an own C# engine and zero third-party vector dependencies.

[![CI](https://github.com/ProvostX-Labs/ProvostXMemory/actions/workflows/build-test-publish.yml/badge.svg)](https://github.com/ProvostX-Labs/ProvostXMemory/actions/workflows/build-test-publish.yml)
[![NuGet](https://img.shields.io/nuget/v/ProvostXMemory)](https://www.nuget.org/packages/ProvostXMemory)

| | |
|---|---|
| **License** | MIT ([LICENSE](LICENSE)) |
| **Target** | .NET 8 |
| **Tests** | 626 green (xUnit, 17 suites), 0 warnings / 0 errors |
| **Native dependencies** | None (pure managed; ILGPU ships in the package — GPU acceleration included, transparent CPU fallback) |

## Why ProvostXMemory

- **One facade.** Hosts see `IMemoryService` + typed options and inject their own embedder, grader, reranker, and telemetry. Storage, indexing, and retrieval strategy are swappable by configuration — never by host code changes.
- **Scope-isolated by construction.** Memory is partitioned per scope (platform / session / tenant / purpose / custom), and cross-scope access is structurally impossible — enforced by path layout and scope-exact registries, pinned by dedicated tests, not by convention.
- **Own vector engine, zero native deps.** Flat (SIMD `Vector512`), IVF, and HNSW are implemented in C#; the **included** ILGPU kernels accelerate Flat/IVF on the GPU when an accelerator is present and fall back to the CPU bit-identically otherwise. No FAISS, no native dylibs, no server.

## Features

| Area | What you get | Key types |
|---|---|---|
| Storage | Transactional JSONL records per scope, manifests, atomic writes | `IMemoryStore`, `JsonlMemoryStore`, `MemoryRootLayout`, `AtomicFileWriter` |
| Vector index | Pluggable backends with a shared conformance suite | `IVectorIndex`, `IVectorIndexFactory`, `IndexKind` |
| Lexical | BM25-lite inverted index with key boosts | `ILexicalIndex`, `Bm25Scorer` |
| Fusion | RRF fusion with dense/lexical weights | `IResultFuser`, `ReciprocalRankFuser` |
| Retrieval | Hybrid, corrective loop, graph, multi-representation, declarative query plans, enforced filters | `IHybridRetriever`, `ICorrectiveRetriever`, `IGraphRetriever`, `IMultiRepresentationRetriever`, `IQueryPlanRunner` |
| Chunking | Structural and size/overlap chunking with parent-document recall | `IChunker`, `StructuredChunker`, `IParentResolver` |
| Graph | Entities, relations, shortest path, cluster summaries | `IMemoryGraphStore`, `IEntityExtractor`, `GraphRetriever` |
| Consolidation | Dedup, TTL expiry, cluster summaries | `IMemoryConsolidator`, `IDedupDetector`, `IClusterSummarizer` |
| Reranking | Heuristic or client-backed reranking | `IReranker`, `ScoreBoostReranker`, `ClientBackedReranker` |
| Correction | Grader-gated retrieval with query rewrite loop | `IRetrievalGrader`, `IQueryRewriter`, `CorrectiveRetriever` |
| Evaluation | Recall@k / Precision@k / MRR / NDCG, thresholds, regression guard, JSON reports | `EvalRunner`, `EvalMetricMath`, `DeterministicEvalCorpus` |
| Logging | Configurable sinks, SHA-256 audit hash chain, retention, content minimization | `MemoryLoggingOptions`, `LogChainVerifier`, `RetentionSweeper` |
| Telemetry | Per-stage timings and scores into a host sink | `IMemoryTelemetry` |

## Quickstart

```csharp
using ProvostXMemory;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Fusion;
using ProvostXMemory.Storage;
using ProvostXMemory.Telemetry;
using ProvostXMemory.VectorIndex.Engine;

var root = "/data/memory-root";
var layout = new MemoryRootLayout(root, "model-hash-v1");

IMemoryService memory = new MemoryServiceBuilder()
    .WithOptions(new MemoryServiceOptions(
        Storage: new StorageOptions(root),
        Vector: new VectorOptions("model-hash-v1", dim: 768, IndexKind.IndexKind.Hnsw),
        Graph: GraphOptions.Default,
        Fusion: new FusionOptions(),
        Consolidation: new ConsolidationOptions()))
    .WithStore(new JsonlMemoryStore(layout))
    .WithEmbedder(embedder)                       // host-injected IVectorEmbedder
    .WithIndexFactory(new EngineVectorIndexFactory())
    .WithFuser(new ReciprocalRankFuser())
    .WithTelemetry(NoopMemoryTelemetry.Instance)
    .Build();

var scope = new MemoryScope(MemoryScopeKind.Session, "user-42");

await memory.RememberAsync(new MemoryRecord(
    "note-1", scope, "helm-rollout", "Kubernetes rollout notes…",
    Category: "ops", Tags: new[] { "infra", "deploy" }));

await memory.BuildIndexesAsync(scope);

RetrievalResult result = await memory.RecallAsync(new RetrievalQuery(
    "kubernetes rollout",
    scope,
    TopK: 5,
    Filters: new RetrievalFilters(Tags: new[] { "deploy" })));

foreach (var hit in result.Records)
{
    Console.WriteLine($"{hit.Score:0.000} {hit.Record.Id}: {hit.Record.Key}");
}
```

## Backend matrix

| Backend | Package | Exactness | Scale guidance | Notes |
|---|---|---|---|---|
| FlatJson (default) | core | exact | up to a few thousand vectors per scope | brute-force cosine, transactional JSON persistence, zero deps |
| FlatExact (SIMD) | `ProvostXMemory.VectorIndex.Engine` | exact | up to ~10k per scope | `Vector512` kernels, own engine |
| IVF | `ProvostXMemory.VectorIndex.Engine` | approximate (tunable `nprobe`) | 10k–1M | deterministic k-means quantizer + inverted lists |
| HNSW | `ProvostXMemory.VectorIndex.Engine` | approximate | 10k–10M | immutable built indexes + batch rebuild; measured recall@10 = 0.998 |
| GPU (ILGPU) | `ProvostXMemory.VectorIndex.Gpu` | exact (Flat, IVF) | Flat/IVF scale | lazy guarded availability; transparent CPU fallback with bit-identical results |

Select via `VectorOptions.IndexKind` (`Flat | Ivf | Hnsw | Gpu`) plus the matching `IVectorIndexFactory`; every backend passes the same conformance suite. Details in [docs/backends.md](docs/backends.md).

## Retrieval pipeline

```
RetrievalQuery ──► routing (plan → corrective → hybrid → rerank)
                     │
        dense vector ┴─ lexical BM25      (per scope, in parallel)
                     │
                RRF fusion (weights, k=60)
                     │
        resolve ids → records (parent-document recall, multi-representation)
                     │
        filter enforcement (tags / category / date window)
                     │
        optional rerank ──► corrective loop (grade → rewrite → retry)
                     │
        RetrievalResult + per-stage diagnostics
```

## Logging and audit hash-chain

Logging is off by default with zero cost. When enabled it offers file/console/injected sinks, JSON-lines format, rotation, retention, strict content minimization (`IncludeContent = false` — record content never appears in logs), and a SHA-256 hash chain that makes any tampering, deletion, or reordering of historical entries verifiable via `LogChainVerifier`. See [docs/logging.md](docs/logging.md).

## Evaluation harness

`ProvostXMemory.Eval` measures Recall@k, Precision@k, MRR, and NDCG over golden query sets against deterministic corpora, checks metric thresholds as a regression guard, and emits canonical diff-friendly JSON reports. The library's own journey tests gate on Recall@5 ≥ 0.9 (achieved 1.0). See [docs/evaluation.md](docs/evaluation.md).

## Documentation

| Document | Contents |
|---|---|
| [docs/getting-started.md](docs/getting-started.md) | Install, first run, DI wiring, backend selection |
| `src/ProvostXMemory.Examples/` | 10 runnable end-to-end examples (quickstart → scopes → hybrid → backends → consolidation → graph → corrective loop → audit logging → eval → restart persistence); run `dotnet run --project src/ProvostXMemory.Examples` |
| [docs/concepts.md](docs/concepts.md) | Records, scopes, representations, chunking, model-hash guard |
| [docs/scopes.md](docs/scopes.md) | Scope kinds, path layout, multi-tenancy patterns |
| [docs/retrieval.md](docs/retrieval.md) | Full pipeline: hybrid, rerank, corrective loop, graph, query plans, filters |
| [docs/backends.md](docs/backends.md) | Per-backend deep dive and selection matrix |
| [docs/consolidation.md](docs/consolidation.md) | Dedup, TTL, cluster summaries, policies |
| [docs/evaluation.md](docs/evaluation.md) | Metrics formulas, harness usage, regression guarding |
| [docs/logging.md](docs/logging.md) | Options reference, hash-chain audit, retention |
| [docs/api-glossary.md](docs/api-glossary.md) | Complete public API glossary, grouped by namespace |
| [docs/faq.md](docs/faq.md) | Frequently asked questions |
| [docs/wiki.html](docs/wiki.html) | Single-file offline documentation browser (open in any browser) |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Layered architecture, backend history, patent addendum |
| [API-INDEX.md](API-INDEX.md) | Generated API inventory |

## Design principles

- Strict OOP: interfaces first, constructor injection, no statics, one type per file; dependencies flow strictly downward through interfaces.
- The library knows nothing about its hosts; hosts know nothing about the internals. No environment variables — all configuration is typed options.
- All file I/O is transactional (temp + atomic swap).
- Every backend passes the same conformance suite; cross-backend parity and determinism are pinned by tests.

## License

Licensed under the [MIT License](LICENSE). Third-party notices: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) (the only third-party dependency is ILGPU, included — GPU acceleration ships in the single ProvostXMemory package with transparent CPU fallback).

| `docs/api-site/` | **Full API reference site** — Microsoft-style, generated by DocFX from XML doc comments: every public type gets a page (hierarchies, members, inheritance). Regenerate with `dotnet tool run docfx metadata && dotnet tool run docfx build`. |
