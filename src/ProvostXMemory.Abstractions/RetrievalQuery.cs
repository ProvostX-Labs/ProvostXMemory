namespace ProvostXMemory.Abstractions;

/// <summary>
/// <para>A scope-bound retrieval request: what to look for, where to look, and how much
/// to return. Passed to <c>IMemoryService.RecallAsync</c>.</para>
/// <para><see cref="Scope"/> is authoritative — results are drawn from that scope's
/// indexes only, never from other scopes. <see cref="Text"/> runs through the full
/// pipeline (embedding + BM25 lexical + fusion + optional rerank/corrective loop);
/// an empty or whitespace text short-circuits to an empty result.
/// <see cref="TopK"/> bounds the returned records. <see cref="Filters"/> (tags,
/// category, date-from) are applied after fusion, so they never affect ranking of
/// the candidates they admit. <see cref="Plan"/> (when set) executes a declarative
/// multi-scope fan-out instead of a single-scope lookup — see <c>QueryPlanRunner</c>.</para>
/// </summary>
public sealed record RetrievalQuery(
    string Text,
    MemoryScope Scope,
    int TopK = 10,
    RetrievalFilters? Filters = null,
    QueryPlan? Plan = null);