# ProvostXMemory.RerankingTests.API.md

Types: 4  |  LOC: 363  |  ~209 tokens

---

Dependencies: ProvostXMemory.Abstractions, ProvostXMemory.Reranking

### Class: ClientBackedRerankerTests
> IClientBackedReranker adapter: id mapping, drops, fallback merge, unknown/duplicate ids.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Reranking, Xunit

### Class: NoopRerankerTests
> NoopReranker identity contract.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Reranking, Xunit

### Class: ScoreBoostRerankerTests
> <see cref="ScoreBoostReranker"/> ordering fixtures: tag/key/category boosts, length
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Reranking, Xunit

### Class: StubRerankerClient
> Test double: reranker client replaying a fixed best-first id order; records requests.
Implements: IRerankerClient
Constructor:
  - StubRerankerClient(params string[] orderedIds)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Reranking
