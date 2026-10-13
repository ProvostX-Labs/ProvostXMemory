using System.Collections.Generic;

namespace ProvostXMemory.Abstractions;


/// <summary>One step of a declarative plan (fan-out namespaces, merge, iterate).</summary>
public sealed record QueryPlanStep(
    string StepId,
    string Action,
    IReadOnlyList<string>? TargetScopes = null,
    int TopK = 10)
{
    private static readonly IReadOnlyList<string> EmptyScopes = new string[0];

    public IReadOnlyList<string> TargetScopes { get; init; } = TargetScopes ?? EmptyScopes;
}
