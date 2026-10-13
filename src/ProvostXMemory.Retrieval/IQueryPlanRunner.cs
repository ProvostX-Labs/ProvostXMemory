using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Retrieval;

/// <summary>Executes a declarative <see cref="QueryPlan"/> (fan-out namespaces, merge, iterate).</summary>
public interface IQueryPlanRunner
{
    Task<RetrievalResult> RunAsync(QueryPlan plan, string text, MemoryScope scope, RetrievalFilters? filters = null, CancellationToken cancellationToken = default);
}