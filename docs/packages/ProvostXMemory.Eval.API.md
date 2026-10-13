# ProvostXMemory.Eval.API.md

Types: 19  |  LOC: 1055  |  ~1156 tokens

---

Dependencies: ProvostXMemory, ProvostXMemory.Abstractions, ProvostXMemory.Consolidation, ProvostXMemory.Correction, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine

### Interface: EvalServiceFactory
> Host-pluggable service construction for eval runs: given a fresh root path the host
Methods:
  - ProvostXMemory.IMemoryService Create(string rootPath)

### Class: ClusterHashEmbedder
> Deterministic fake embedder for eval runs — NOT a real model. Cluster-preserving:
Implements: IVectorEmbedder
Constructor:
  - ClusterHashEmbedder(int dim = 128, float noiseAmplitude = 0.05f)
Cross-package deps: ProvostXMemory.VectorIndex

### Class: DefaultEvalServiceFactory
> Default eval wiring: a real <see cref="MemoryService"/> over the transactional JSONL
Implements: EvalServiceFactory
Constructor:
  - DefaultEvalServiceFactory(int vectorDim = 128)
Cross-package deps: ProvostXMemory, ProvostXMemory.Consolidation, ProvostXMemory.Correction, ProvostXMemory.Fusion, ProvostXMemory.Graph, ProvostXMemory.Storage, ProvostXMemory.Telemetry, ProvostXMemory.VectorIndex, ProvostXMemory.VectorIndex.Engine, ProvostXMemory.Abstractions

### Class: DeterministicEvalCorpus
> Seeded synthetic corpus: N records per scope across M scopes, round-robin assigned to
Constructor:
  - DeterministicEvalCorpus(int seed, int scopeCount, int recordCountPerScope, int clusterCount = 8, int vectorDim = DefaultVectorDim)
Cross-package deps: ProvostXMemory.Abstractions

### Class: EvalCase
> One golden retrieval case: a query bound to a scope plus the record ids a good
Constructor:
  - EvalCase(string id, string query, MemoryScope queryScope, IReadOnlyList<string> relevantIds, IReadOnlyList<double>? gradedRelevance = null)
Cross-package deps: ProvostXMemory.Abstractions

### Class: EvalCaseSet
> A named set of golden eval cases grouped under one logical set id. Case ids must be
Constructor:
  - EvalCaseSet(string id, IReadOnlyList<EvalCase> cases)
Cross-package deps: ProvostXMemory.Abstractions

### Class: EvalMetricMath
> The metric formulas, implemented independently of any search engine and unit-tested
Cross-package deps: ProvostXMemory.Abstractions

### Class: EvalMetricThresholds
> Minimum metric values for the aggregate <see cref="EvalReport"/>. A violation is any
Constructor:
  - EvalMetricThresholds(double? recallAtK = null, double? precisionAtK = null, double? mrr = null, double? ndcgAtK = null)

### Class: EvalReportJson
> Serializable result of running an <see cref="EvalCaseSet"/>: per-case metrics, the

### Class: EvalRunner
> Runs an <see cref="EvalCaseSet"/> against a freshly built service from an
Constructor:
  - EvalRunner(EvalServiceFactory serviceFactory, EvalMetricMath? metricMath = null)
Cross-package deps: ProvostXMemory.Abstractions

### Class: EvalRunnerOptions
> Host-pluggable service construction for eval runs: given a fresh root path the host
Constructor:
  - EvalRunnerOptions(int topK = DefaultTopK, EvalMetricThresholds? thresholds = null, bool disposeRootDirectory = true)

### Class: MemoryScopeComparer
> Ordinal scope comparison helper (Kind + Id); shared by eval components.
Implements: MemoryScope>

### Class: MetricThresholdViolations
> Minimum metric values for the aggregate <see cref="EvalReport"/>. A violation is any
Constructor:
  - MetricThresholdViolations(IReadOnlyList<MetricThresholdViolation> violations)

### Record: CorpusRecord
> Seeded synthetic corpus: N records per scope across M scopes, round-robin assigned to
Constructor:
  - CorpusRecord(MemoryScope Scope, MemoryRecord Record, int ClusterIndex)
Cross-package deps: ProvostXMemory.Abstractions

### Record: EvalCaseMetrics
> Metrics for one eval case: Recall@k, Precision@k, MRR and NDCG@k over the ranked
Constructor:
  - EvalCaseMetrics(string CaseId, string ScopeKind, string ScopeId, double RecallAtK, double PrecisionAtK, double Mrr, double NdcgAtK, int ResultCount, int RelevantRetrievedCount, int RelevantTotalCount)

### Record: EvalCaseTrace
> Serializable result of running an <see cref="EvalCaseSet"/>: per-case metrics, the
Constructor:
  - EvalCaseTrace(string CaseId, string ScopeKind, string ScopeId, int ForeignRecordHits, IReadOnlyList<EvalCaseTraceHit> Hits)

### Record: EvalCaseTraceHit
> Serializable result of running an <see cref="EvalCaseSet"/>: per-case metrics, the
Constructor:
  - EvalCaseTraceHit(string RecordId, double Gain)

### Record: EvalMetricBreakdownEntry
> Metrics for one eval case: Recall@k, Precision@k, MRR and NDCG@k over the ranked
Constructor:
  - EvalMetricBreakdownEntry(string Key, EvalMetrics Metrics, IReadOnlyList<string> CaseIds)

### Record: MetricThresholdViolation
> Minimum metric values for the aggregate <see cref="EvalReport"/>. A violation is any
Constructor:
  - MetricThresholdViolation(string Metric, double Actual, double Required)
