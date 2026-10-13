# ProvostXMemory

A strict-OOP, host-agnostic long-term memory library for .NET — scoped storage, hybrid (dense + lexical) retrieval with enforced filters, consolidation, graph, corrective recall, an eval harness, and audit-grade integrity-logged observability — over pluggable vector-index backends with an own C# engine and zero third-party vector dependencies.

This is the **site home** of the generated offline documentation. Start with:

- [README](README.md) — full landing page: features, quickstart, backend matrix, design principles
- [docs/getting-started.md](docs/getting-started.md) — install, first run, DI wiring, backend selection
- [docs/concepts.md](docs/concepts.md) — records, scopes, representations, chunking, model-hash guard
- [docs/scopes.md](docs/scopes.md) — scope kinds, path layout, multi-tenancy patterns
- [docs/retrieval.md](docs/retrieval.md) — full retrieval pipeline: hybrid, rerank, corrective loop, graph
- [docs/backends.md](docs/backends.md) — per-backend deep dive and selection matrix
- [docs/consolidation.md](docs/consolidation.md) — dedup, TTL, cluster summaries
- [docs/evaluation.md](docs/evaluation.md) — metrics, harness, regression guarding
- [docs/logging.md](docs/logging.md) — options reference, hash-chain audit, retention
- [docs/api-glossary.md](docs/api-glossary.md) — complete public API glossary
- [docs/faq.md](docs/faq.md) — frequently asked questions
- API reference — every public type: start at [ProvostXMemory](xref:ProvostXMemory) (facade) or [ProvostXMemory.Abstractions](xref:ProvostXMemory.Abstractions) (contracts)
- [ARCHITECTURE.md](ARCHITECTURE.md), [API-INDEX.md](API-INDEX.md), [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)

The single-file offline wiki browser ships at [docs/wiki.html](docs/wiki.html); the full source tree (including the runnable examples under `src/ProvostXMemory.Examples/`) is in the repository.

Licensed under the [MIT License](LICENSE).