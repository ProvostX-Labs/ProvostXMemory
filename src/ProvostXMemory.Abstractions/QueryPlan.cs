using System.Collections.Generic;

namespace ProvostXMemory.Abstractions;


/// <summary>Declarative retrieval plan: an ordered set of steps the runner executes.</summary>
public sealed record QueryPlan(
    string PlanId,
    IReadOnlyList<QueryPlanStep>? Steps = null)
{
    private static readonly IReadOnlyList<QueryPlanStep> EmptySteps = new QueryPlanStep[0];

    public IReadOnlyList<QueryPlanStep> Steps { get; init; } = Steps ?? EmptySteps;
}

