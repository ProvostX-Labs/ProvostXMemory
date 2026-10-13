# ProvostXMemory.Consolidation.API.md

Types: 7  |  LOC: 475  |  ~506 tokens

---

Dependencies: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex

### Interface: IClusterSummarizer
> Summarizes a cluster of records into a new summary record.
Methods:
  - Task<MemoryRecord> SummarizeAsync(string scopeKind, string scopeId, IReadOnlyList<MemoryRecord> cluster, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IDedupDetector
> Near-duplicate detection via similarity threshold.
Methods:
  - Task<bool> IsDuplicateAsync(MemoryRecord left, MemoryRecord right, double threshold, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: IMemoryConsolidator
> Consolidation pass over a scope: dedup, merge, TTL expiry, cluster summaries.
Methods:
  - Task<IReadOnlyList<MemoryRecord>> ConsolidateAsync(string scopeKind, string scopeId, ConsolidationOptions options, CancellationToken cancellationToken = default)
Cross-package deps: ProvostXMemory.Abstractions

### Class: ClusterSummarizer
> Builds a summary <see cref="MemoryRecord"/> from a near-duplicate cluster: the
Implements: IClusterSummarizer
Constructor:
  - ClusterSummarizer(ConsolidationTuning? tuning = null)
Cross-package deps: ProvostXMemory.Abstractions

### Class: ConsolidationReportMeta
> Side channel that attaches the pass <see cref="ConsolidationReport"/> to the records
Cross-package deps: ProvostXMemory.Abstractions

### Class: MemoryConsolidator
> One consolidation pass over a scope, executed against the scope-bound
Implements: IMemoryConsolidator
Constructor:
  - MemoryConsolidator(IMemoryStore store, IDedupDetector dedupDetector, IVectorEmbedder embedder, IClusterSummarizer clusterSummarizer, ConsolidationTuning? tuning = null)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex

### Class: ThresholdDedupDetector
> Near-duplicate detection: cosine over records embedded via the injected
Implements: IDedupDetector
Constructor:
  - ThresholdDedupDetector(IVectorEmbedder embedder)
Cross-package deps: ProvostXMemory.Abstractions, ProvostXMemory.VectorIndex
