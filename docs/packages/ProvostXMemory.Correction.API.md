# ProvostXMemory.Correction.API.md

Types: 8  |  LOC: 174  |  ~518 tokens

---

Dependencies: ProvostXMemory.Abstractions, ProvostXMemory.Retrieval

### Interface: IExternalFetcher
> Fetches content from external sources (host-injected; the library stays network-agnostic).
Methods:
  - Task<IReadOnlyList<string>?> FetchAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IQueryRewriter
> Rewrites/expands a query for corrective retrieval (host-injected when an LLM-backed rewriter is desired).
Methods:
  - Task<string> RewriteAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IRetrievalGrader
> Grade of a retrieved candidate.
Methods:
  - Task<GradedCandidate> GradeAsync(RetrievalQuery query, ScoredMemoryRecord candidate, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Class: CorrectiveRetriever
> Grader-gated corrective retrieval around an injected <see cref="IHybridRetriever"/>:
Implements: ICorrectiveRetriever
Constructor:
  - CorrectiveRetriever(IHybridRetriever hybridRetriever, IRetrievalGrader grader, IQueryRewriter rewriter, IExternalFetcher? fetcher = null, CorrectionOptions? options = null)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Retrieval

### Class: KeywordQueryRewriter
> Deterministic, zero-dependency query rewriter: distills the query to its key terms via
Implements: IQueryRewriter
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Retrieval

### Class: StageNames
> Grader-gated corrective retrieval around an injected <see cref="IHybridRetriever"/>:
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Retrieval

### Record: CorrectionOptions
> Options for the corrective retrieval pipeline. Typed options injected by the host
Constructor:
  - CorrectionOptions(double RelevanceFloor = 0.0, bool UseFetcherOnEmpty = true)

### Record: GradedCandidate
> Grade of a retrieved candidate.
Constructor:
  - GradedCandidate(ScoredMemoryRecord Candidate, RetrievalGrade Grade)
Cross-package deps: ProvostXMemory.Abstractions
