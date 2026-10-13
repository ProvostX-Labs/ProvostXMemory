# ProvostXMemory.Reranking.API.md

Types: 7  |  LOC: 222  |  ~503 tokens

---

Dependencies: ProvostXMemory.Abstractions

### Interface: IReranker
> Reranks candidates against a query (host may inject an LLM/cross-encoder-backed implementation).
Methods:
  - Task<IReadOnlyList<ScoredMemoryRecord>> RerankAsync(RetrievalQuery query,
        IReadOnlyList<ScoredMemoryRecord> candidates,
        CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IRerankerClient
> Optional external rerank service contract (LLM/cross-encoder API, host-injected).
Methods:
  - Task<IReadOnlyList<string>> RerankAsync(string query, IReadOnlyList<string> candidateIds, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Class: ClientBackedReranker
> Adapter wrapping the host-injected <see cref="IRerankerClient"/>: the client returns
Implements: IReranker
Constructor:
  - ClientBackedReranker(IRerankerClient client, IReranker? fallback = null, ClientBackedRerankerOptions? options = null)
Cross-package deps: ProvostXMemory.Abstractions

### Class: NoopReranker
> Identity reranker: returns candidates unchanged (deterministic pass-through).
Implements: IReranker
Cross-package deps: ProvostXMemory.Abstractions

### Class: ScoreBoostReranker
> Deterministic, zero-dependency reranker: additive boosts for tag/key/category matches
Implements: IReranker
Constructor:
  - ScoreBoostReranker(ScoreBoostRerankerOptions? options = null)
Cross-package deps: ProvostXMemory.Abstractions

### Record: ClientBackedRerankerOptions
> Options for <see cref="ClientBackedReranker"/>: which client ids to trust and where
Constructor:
  - ClientBackedRerankerOptions(bool TrustClientCompletely = true)

### Record: ScoreBoostRerankerOptions
> Options for <see cref="ScoreBoostReranker"/>. Deterministic, zero-dependency tuning.
Constructor:
  - ScoreBoostRerankerOptions(double TagMatchBoost = 0.15, double KeyTermBoost = 0.10, double CategoryBoost = 0.05, double ShortLengthPenalty = 0.25, double LongLengthPenalty = 0.25, int MinLength = 8, int MaxLength = 4000)
