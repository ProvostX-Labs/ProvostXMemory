using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Eval;

/// <summary>
/// A named set of golden eval cases grouped under one logical set id. Case ids must be
/// unique within a scope (cross-scope ids may repeat); the set is immutable after creation.
/// </summary>
public sealed class EvalCaseSet
{
    private readonly IReadOnlyList<EvalCase> _cases;

    public EvalCaseSet(string id, IReadOnlyList<EvalCase> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Eval set id is required.", nameof(id));
        }

        var copy = cases.ToList();
        Validate(copy);
        _cases = copy;
        Id = id;
    }

    public string Id { get; }

    public IReadOnlyList<EvalCase> Cases => _cases;

    /// <summary>Distinct scopes referenced by the cases, in first-appearance order.</summary>
    public IReadOnlyList<MemoryScope> Scopes()
    {
        var seen = new List<MemoryScope>();
        foreach (var evalCase in _cases)
        {
            var scope = evalCase.QueryScope;
            if (!seen.Contains(scope, MemoryScopeComparer.Ordinal))
            {
                seen.Add(scope);
            }
        }

        return seen;
    }

    /// <summary>Returns a new set with the given cases appended (uniqueness revalidated).</summary>
    public EvalCaseSet WithCases(IEnumerable<EvalCase> additional)
    {
        ArgumentNullException.ThrowIfNull(additional);
        var merged = new List<EvalCase>(_cases);
        merged.AddRange(additional);
        return new EvalCaseSet(Id, merged);
    }

    // Pure validation over an immutable list; intentionally static per architecture rule.
    private static void Validate(IReadOnlyList<EvalCase> cases)
    {
        var seenIds = new HashSet<((MemoryScopeKind Kind, string Id) Scope, string CaseId)>();
        for (var i = 0; i < cases.Count; i++)
        {
            var evalCase = cases[i]
                ?? throw new ArgumentException($"Case {i} is null.", nameof(cases));
            var key = (evalCase.QueryScope.Kind, evalCase.QueryScope.Id);
            var idKey = (key, evalCase.Id);
            if (!seenIds.Add(idKey))
            {
                throw new ArgumentException(
                    $"Duplicate case id '{evalCase.Id}' in scope '{evalCase.QueryScope.Kind}/{evalCase.QueryScope.Id}'.");
            }
        }
    }
}