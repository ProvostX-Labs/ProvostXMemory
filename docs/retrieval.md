# Retrieval

How a query becomes an answer: the pipeline stages, when to use each retrieval route, and how filters apply.

## Pipeline overview

```
                       RetrievalQuery (text, scope, topK, filters, plan?)
                                        |
                     ┌──────────────────┴──────────────────┐
                     |  MemoryService.RecallAsync routing  |
                     |  plan → corrective → hybrid → rerank|
                     └──────────────────┬──────────────────┘
                                        |
        ┌───────────────┬───────────────┴────────────────┐
        ▼               ▼                                ▼
   dense vector     lexical (BM25)                  query plan fan-out
   search per       search per scope                across target scopes
   scope            + key boost
        └───────┬───────┘
                ▼
        fusion (RRF, k=60 default; weighted dense/lexical)
                ▼
        resolve vector/chunk ids → memory records (parent-document recall)
                ▼
        filter enforcement (tags / category / date window)
                ▼
        optional rerank (IReranker)
                ▼
        corrective loop (when a grader is wired): grade → rewrite → retry
                ▼
        RetrievalResult + diagnostics (per-stage scores, timings)
```

Every stage is scope-exact. The scope index registry resolves the vector index, lexical index, and record provider for the query's scope; nothing from another scope enters the funnel.

## Hybrid retrieval (default route)

`HybridRetriever` (implements `IHybridRetriever`) runs dense and lexical search in parallel for the query scope, then fuses:

- **Dense** — the query is embedded with the host's `IVectorEmbedder` and searched against the scope's vector index.
- **Lexical** — the tokenized query is scored against the scope's BM25 index (`Bm25Scorer`, saturated-TF IDF weighting plus a key-term boost).
- **Fusion** — `ReciprocalRankFuser` combines the ranked lists with Reciprocal Rank Fusion (`FusionOptions.RrfK`, default 60) and configurable `DenseWeight` / `LexicalWeight`. Zero weights drop a stage entirely; a deterministic fallback ordering handles the all-zero edge case.
- **Resolve** — vector ids (including `repr|…` multi-representation ids and chunk ids) resolve back to records via the per-record id ledger and parent resolver.
- **Rerank** — if an `IReranker` is wired, candidates are re-scored after fusion.

Use this route when you want the standard quality/speed balance with no extra infrastructure.

## Reranking

Three built-in `IReranker` implementations:

| Reranker | Behavior |
|---|---|
| `NoopReranker` | Pass-through; default when none is wired. |
| `ScoreBoostReranker` | Heuristic boosts for tag matches, key-term hits, and category matches; length-based penalties (`ScoreBoostRerankerOptions`). |
| `ClientBackedReranker` | Delegates to an `IRerankerClient` (your cross-encoder/LLM service) with a fallback `IReranker`; `ClientBackedRerankerOptions.TrustClientCompletely` controls whether client order is taken verbatim or fused with the fallback. |

The library stays LLM-agnostic: reranking intelligence is injected, never built in.

## Corrective loop

`CorrectiveRetriever` (implements `ICorrectiveRetriever`) wraps the hybrid retriever with a self-checking loop:

1. Retrieve candidates via the hybrid retriever.
2. `IRetrievalGrader` grades each candidate `Relevant | Irrelevant | Uncertain` (host-injected — typically an LLM call).
3. If evidence is weak (below `CorrectionOptions.RelevanceFloor`), `IQueryRewriter` (default `KeywordQueryRewriter`) rewrites the query and retries.
4. If the result set is empty and `UseFetcherOnEmpty` is set, the injected `IExternalFetcher` is invoked — the extension hook for web augmentation. The library never fetches on its own.

Use this route when recall quality matters more than latency, and you have a grading signal available.

## Graph retrieval

The graph layer stores entities and relations extracted from memory records (via `IEntityExtractor` — `HeuristicEntityExtractor` ships in-box; hosts may inject an LLM-backed extractor) into a scope-pinned `JsonlMemoryGraphStore`.

`GraphRetriever` (implements `IGraphRetriever`) maps a query to relevant entities, traverses `Neighbors` / `ShortestPath`, and returns records from the relevant subgraph together with `GraphCluster` summaries. Consolidation-emitted cluster summaries also surface in ordinary recall results as records with category `summary` and are linked as graph clusters.

Use graph retrieval when questions are relational ("who is connected to X", "what cluster does this belong to") rather than purely similarity-based.

## Query plans

A `QueryPlan` is a declarative, ordered list of `QueryPlanStep` entries, each with an `Action`, `TargetScopes` (scope ids to fan out to), and `TopK`. `QueryPlanRunner` executes the steps:

- Fan out retrieval across target scopes, with **scope-qualified fusion ids** so identical record ids in different scopes stay distinct.
- Merge step results through the fuser.
- One weak-evidence retry: if merged evidence is thin, the query is rewritten once (`IQueryTextRewriter`, default `KeywordQueryTextRewriter`) and re-executed.

Use a plan when one logical question spans multiple scopes (e.g. a session scope plus its tenant parent) or when the agentic surface should express retrieval as data. `MemoryService.RecallAsync` routes to the plan runner automatically when `RetrievalQuery.Plan` is set.

## Filters

`RetrievalFilters` supports tag sets, a single category, and a date window (`From` / `To`). Filters are **enforced** after candidate resolution (`RetrievalFilterApplier`), not merely hinted — filtering is pinned by dedicated enforcement tests. Filter on tags/category/date when the candidate set should be narrowed semantically *and* structurally.

## Diagnostics

`RetrievalResult.Diagnostics` exposes per-stage scores and timings; `IMemoryTelemetry` receives the same signals for host-side observability. Stage names are stable constants (`StageNames`).
