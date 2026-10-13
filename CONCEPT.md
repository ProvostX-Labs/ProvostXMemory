# ProvostXMemory — Concept

**License:** MIT.

## What it is

A standalone .NET library that owns ALL memory and retrieval concerns for any host
application (Agent satellite, Admin platform, future products). Hosts see one clean
surface (`IMemoryService` + options) and never know what storage, indexing, or
retrieval strategy is running underneath. Swappable by config, nothing else.

## What problem it solves

Every ProvostX project ends up re-implementing retrieval (the old Agent rolled its
own flat-JSON vector index + BM25-lite; the Knowledge repo has FAISS + a shell
script). Memory logic should live in ONE atomic, encapsulated library:

- Hosts reference one NuGet package, wire it via constructor injection, done
- Backends (flat JSON, FAISS, sqlite-vec) are pluggable — chosen by config
- Multi-tenant isolation: memory is separated by platform / session / tenant / purpose
  via namespaces. Different users, sessions, or products NEVER see each other's memories
- Full retrieval stack: chunking, hybrid lexical+vector search, fusion, reranking,
  corrective loops, graph traversal, multi-representation records

## Core capabilities (RAG architecture coverage)

- **Hybrid retrieval:** BM25 lexical + dense vector search with RRF/weighted fusion
- **Chunking policies:** structural/size/overlap chunking + parent-document recall
- **Graph layer:** entities + relations over memory records; subgraph retrieval, cluster summaries
- **Corrective loop:** retrieval evaluator hook (grade → keep / web-out / rewrite query) — grader injected by host, library stays LLM-agnostic
- **Agentic surface:** retrieval as a declarative query-plan (fan out namespaces, merge, iterate on weak evidence)
- **Multi-representation records:** one record, many representations (text / image caption / table), per-modality embeddings in one namespace

## Backend matrix

- `FlatJson` — zero-dependency default (ported from the old Agent; brute-force cosine)
- `FaissManaged` — pure-.NET FAISS port (Gravicode.FaissNet), CPU SIMD
- `FaissGpu` — ILGPU (CUDA/OpenCL) with CPU fallback, later tier
- Native FAISS dylib matrix via P/Invoke — optional, only if managed port falls short

## Isolation model

Root folder per host install; inside it, namespace segments:
`<root>/<scope-kind>/<scope-id>/<model-hash>/…`
Scope kinds: platform (telegram/discord/…), session, tenant, custom purpose. Every
store/index/graph operation is scope-bound; cross-scope reads are impossible by
construction, not by convention.

## Who uses it

Hosts that need durable, recallable memory: the Agent satellite (sessions, curated
memory), Admin platform (tenant search), any future product. Integration cost: one
package reference + a few config lines + DI wiring.

## Non-goals

- Hosting web search itself (hosts provide external fetchers via interface)
- Choosing embedding models (hosts inject `IVectorEmbedder`; library validates hashes/dims)
- UI of any kind
