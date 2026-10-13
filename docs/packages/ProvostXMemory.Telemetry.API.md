# ProvostXMemory.Telemetry.API.md

Types: 3  |  LOC: 118  |  ~182 tokens

---

Dependencies: ProvostXMemory.Abstractions

### Interface: IMemoryTelemetry
> Query/consolidation timings, per-stage scores, backend stats (host-injected sink; default no-op).
Methods:
  - Task ReportStageAsync(string stageName, double durationMs, IReadOnlyDictionary<string, double> scores)
  - Task ReportBackendStatsAsync(BackendStats stats)
Cross-package deps: ProvostXMemory.Abstractions

### Class: InMemoryMemoryTelemetry
> Capture-style telemetry sink that records reported stages and backend stats in
Implements: IMemoryTelemetry
Cross-package deps: ProvostXMemory.Abstractions

### Class: NoopMemoryTelemetry
> Zero-op default telemetry sink. Never throws, never allocates meaningfully.
Implements: IMemoryTelemetry
Cross-package deps: ProvostXMemory.Abstractions
