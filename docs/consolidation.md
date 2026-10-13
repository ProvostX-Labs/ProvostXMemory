# Consolidation

Consolidation is the maintenance pass that keeps a scope's memory healthy: deduplication, TTL expiry, and cluster-summary emission. It is triggered explicitly via `IMemoryService.ConsolidateAsync(scope, options)` — the library never mutates memory in the background on its own.

## What a pass does

`MemoryConsolidator` (implements `IMemoryConsolidator`) runs over one scope:

1. **Deduplication** — `ThresholdDedupDetector` (implements `IDedupDetector`) finds near-duplicates by embedding similarity at or above `ConsolidationOptions.DedupSimilarityThreshold`. Survivors are the **oldest** record; the survivor's tags are the union of the duplicate set's tags; newer duplicates are removed and their index artifacts are cleaned up.
2. **TTL expiry** — when `ConsolidationOptions.ExpireTtl` is set, records whose `ExpiresAt` is in the past are deleted; expiry persists across restarts.
3. **Cluster summaries** — when `ConsolidationOptions.EmitClusterSummaries` is set, `ClusterSummarizer` (implements `IClusterSummarizer`) produces a new summary record for each coherent cluster:
   - the summary record is stored with category `summary` and appears in ordinary recall results;
   - the cluster is also recorded in the scope's graph store (`IMemoryGraphStore` cluster write path), linking the member entities.

The pass returns a report (via `ConsolidationReportMeta`) describing removals and emissions, and the facade refreshes or removes affected index artifacts so recall immediately reflects the consolidated state.

## Options reference

| Option | Default | Effect |
|---|---|---|
| `DedupSimilarityThreshold` | high (≈0.99999 used in journeys) | Similarity at which two records are considered duplicates. |
| `ExpireTtl` | `true` | Delete records past `ExpiresAt` during the pass. |
| `EmitClusterSummaries` | `true` | Generate summary records + graph clusters. |

`ConsolidationTuning` exposes additional internal tuning knobs to the consolidator implementation.

## Policies and recommended cadence

- **Threshold choice:** set the dedup threshold as high as precision allows. At 0.99999+ only true restatements merge; lowering it aggressively risks collapsing distinct memories.
- **When to run:** after bulk ingestion, periodically for long-lived session scopes, and before eval runs that should measure consolidated-state quality.
- **Ordering guarantees:** consolidation is deterministic — the same store state and options produce the same survivors, unions, and summaries (pinned by tests, including non-regression of golden-query scores across a consolidation pass).
- **Index consistency:** you do not need to rebuild after consolidation; the facade refreshes index artifacts as part of the pass. A full `RebuildAsync` remains available for wholesale reindexing.
