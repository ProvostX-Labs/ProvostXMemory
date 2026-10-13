# ProvostXMemory.EvalTests.API.md

Types: 7  |  LOC: 741  |  ~318 tokens

---

Dependencies: ProvostXMemory.Abstractions, ProvostXMemory.Eval

### Class: ClusterHashEmbedderTests
> Deterministic fake embedder: pure function of text (no randomness), cluster-preserving
Cross-package deps: ProvostXMemory.Eval, Xunit

### Class: DefaultEvalServiceFactoryTests
> Default factory wiring: host-agnostic construction of a real service on a temp root.
Cross-package deps: ProvostXMemory.Eval, Xunit

### Class: DeterministicEvalCorpusTests
> Corpus determinism (same seed → same hash), cluster balance, expected-match derivability.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Eval, Xunit

### Class: EvalMetricMathTests
> Metric formulas verified against hand-computed values (no search engine involved):
Cross-package deps: ProvostXMemory.Eval, Xunit

### Class: EvalModelTests
> EvalCase / EvalCaseSet validation, graded relevance, scope grouping.
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Eval, Xunit

### Class: EvalRunnerTests
> EvalRunner end-to-end on the real default pipeline: load → index → evaluate,
Implements: IDisposable
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.Eval, Xunit

### Class: RecallRegressionGuardTests
> THE recall@k regression fixture the architecture calls for: the default hybrid pipeline
Cross-package deps: ProvostXMemory.Eval, Xunit
