# ProvostXMemory.CorrectionTests.API.md

Types: 6  |  LOC: 469  |  ~408 tokens

---

Dependencies: ProvostXMemory.Abstractions, ProvostXMemory.Correction, ProvostXMemory.Retrieval

### Class: CorrectiveRetrieverTests
> Grader-mock driven paths of <see cref="CorrectiveRetriever"/>: pass-through, drop,
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Correction, Xunit

### Class: KeywordQueryRewriterTests
> KeywordQueryRewriter: stopword stripping, acronym preservation, dedup, determinism.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Correction, Xunit

### Class: RecordingQueryRewriter
> Test double: rewriter returning a fixed text; records whether it ran and on what input.
Implements: IQueryRewriter
Constructor:
  - RecordingQueryRewriter(string rewrittenText)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Correction

### Class: StubExternalFetcher
> Test double: external fetcher returning a fixed snippet list; records calls.
Implements: IExternalFetcher
Constructor:
  - StubExternalFetcher(params string[] snippets)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Correction

### Class: StubHybridRetriever
> Test double: hybrid retriever replaying queued results; records each query text.
Implements: IHybridRetriever
Constructor:
  - StubHybridRetriever(params RetrievalResult[] results)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Retrieval

### Class: StubRetrievalGrader
> Test double: grades candidates by record id (default grade for unknown ids); records calls.
Implements: IRetrievalGrader
Constructor:
  - StubRetrievalGrader(IReadOnlyDictionary<string, RetrievalGrade> grades, RetrievalGrade defaultGrade = RetrievalGrade.Relevant)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Correction
