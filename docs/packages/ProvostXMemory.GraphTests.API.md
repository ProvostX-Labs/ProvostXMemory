# ProvostXMemory.GraphTests.API.md

Types: 4  |  LOC: 559  |  ~217 tokens

---

Dependencies: ProvostXMemory.Abstractions, ProvostXMemory.Graph, ProvostXMemory.Storage

### Class: GraphRetrieverTests
> Graph retriever: seed by entity/tag, hop decay ordering, cluster summaries, scope pinning.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Graph, ProvostXMemory.Storage, Xunit

### Class: GraphShortestPathTests
> BFS shortest path: direct edge, 2-hop path, disconnected nodes, self path.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Graph, ProvostXMemory.Storage, Xunit

### Class: HeuristicEntityExtractorTests
> Heuristic extractor: capitalized phrases, tags/keywords, co-occurrence relations, idempotence.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Graph, Xunit

### Class: JsonlMemoryGraphStoreTests
> Store round-trip, merge semantics and scope isolation on temp-root fixtures.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Graph, ProvostXMemory.Storage, Xunit
