# Getting Started

This guide walks through installing ProvostXMemory, wiring it into a host application, and selecting a vector index backend.

## Requirements

- .NET 8 (the library targets the .NET 8 SDK; any runtime compatible with .NET 8 works)
- No native dependencies, no external services, no database server. Storage is files on disk; the vector engine is pure C#.

## Install

The public package id is `ProvostXMemory` (packaging is owner-gated; until published, reference the projects directly):

```text
dotnet add package ProvostXMemory
```

Hosts reference **only** the root package. Optional backend packages are added when a non-default index kind is selected:

| Package | Provides |
|---|---|
| `ProvostXMemory` | Facade, abstractions, storage, lexical, fusion, chunking, graph, consolidation, correction, reranking, retrieval, eval, logging, telemetry, FlatJson backend |
| `ProvostXMemory.VectorIndex.Engine` | Own vector engine: FlatExact (SIMD), IVF, HNSW |
| `ProvostXMemory.VectorIndex.Gpu` | ILGPU GPU kernels (Flat + IVF) with transparent CPU fallback |

## First run

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
```

Everything the host injects is an interface: `IVectorEmbedder` (your embedding model), `IVectorIndexFactory` (the backend), optionally `IRetrievalGrader`, `IQueryRewriter`, `IReranker`, `IEntityExtractor`, `IMemoryTelemetry`, and `IMemoryLogSink`. The library never picks models or calls out to the network on its own.

### Write and read

```csharp
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

Key semantics to know on day one:

- `BuildIndexesAsync(scope)` must be called (or `RebuildAsync()` for all scopes) before recall returns results — index state is cold until built, and rebuilt deterministically after a restart.
- Every record, query, and index artifact is bound to a `MemoryScope`. A query in one scope can never return records from another.

## Dependency-injection wiring

`MemoryServiceBuilder` is a fluent convenience wrapper; underneath it is plain constructor injection, so it composes naturally with any DI container:

```csharp
services.AddSingleton(sp =>
{
    var root = configuration["Memory:Root"]!;
    var layout = new MemoryRootLayout(root, configuration["Memory:ModelHash"]!);
    return new MemoryServiceBuilder()
        .WithOptions(new MemoryServiceOptions(
            Storage: new StorageOptions(root),
            Vector: new VectorOptions(
                configuration["Memory:ModelHash"]!,
                dim: 768,
                IndexKind.IndexKind.Hnsw),
            Graph: GraphOptions.Default,
            Fusion: new FusionOptions(),
            Consolidation: new ConsolidationOptions(),
            Logging: new MemoryLoggingOptions(
                Enabled: true,
                Level: MemoryLogLevel.Info,
                Sink: MemoryLogSinkKind.Injected,
                Integrity: MemoryLogIntegrity.HashChain)))
        .WithStore(new JsonlMemoryStore(layout))
        .WithEmbedder(sp.GetRequiredService<IVectorEmbedder>())
        .WithIndexFactory(new EngineVectorIndexFactory())
        .WithFuser(new ReciprocalRankFuser())
        .WithTelemetry(NoopMemoryTelemetry.Instance)
        .WithLogSink(sp.GetRequiredService<IMemoryLogSink>())
        .Build();
});
services.AddSingleton<IMemoryService>(sp => sp.GetRequiredService<MemoryServiceBuilder>().Build());
```

Register the embedder once; it is the single place where your embedding model enters the system. The `model-hash` in `VectorOptions` and `MemoryRootLayout` must match the model you inject — the library guards against dimension and model-hash mismatches when opening index artifacts (see [Concepts](concepts.md#model-hash-guard)).

## Backend selection

Choose `VectorOptions.IndexKind` and the matching factory:

| Kind | Factory | Package | Choose when |
|---|---|---|---|
| `Flat` | `EngineVectorIndexFactory` | Engine | Exact search up to ~10k vectors; SIMD-accelerated |
| `Ivf` | `EngineVectorIndexFactory` | Engine | 10k–1M vectors; small recall trade-off for much faster search |
| `Hnsw` | `EngineVectorIndexFactory` | Engine | 10k–10M vectors; approximate, high recall (measured 0.998 @10) |
| `Gpu` | `GpuVectorIndexFactory` | Gpu | Flat/IVF workloads with a compatible accelerator; falls back to CPU transparently |
| default FlatJson | core (no factory wiring needed beyond a scope-aware factory) | core | Zero-dependency persistence-first deployments; brute-force cosine per scope |

All backends implement the same `IVectorIndex` contract and pass an identical conformance suite, so switching is a configuration change, not a code change. See [Backends](backends.md) for the full comparison.
