using System;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Retrieval;

namespace ProvostXMemory;

/// <summary>
/// Default plan-fan-out scope resolution: a target string names either the root scope
/// itself ("<c>&lt;Kind&gt;/&lt;Id&gt;</c>", case-insensitive) or a child scope built via
/// <see cref="MemoryScope.WithParent"/> ("<c>&lt;ChildKind&gt;/&lt;ChildId&gt;</c>" over
/// the root). Resolving a child pins the parent, so fan-out targets are always
/// descendants or the root itself — never unrelated scopes.
/// </summary>
public sealed class RootChildScopeResolver : QueryPlanRunner.IScopeResolver
{
    /// <inheritdoc />
    public MemoryScope Resolve(MemoryScope rootScope, string targetName)
    {
        ArgumentNullException.ThrowIfNull(rootScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);

        var separator = targetName.IndexOf('/', StringComparison.Ordinal);
        if (separator <= 0 || separator == targetName.Length - 1)
        {
            throw new ArgumentException($"Plan target '{targetName}' is not a '<Kind>/<Id>' pair.", nameof(targetName));
        }

        var kindPart = targetName[..separator];
        var idPart = targetName[(separator + 1)..];
        if (!Enum.TryParse<MemoryScopeKind>(kindPart, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            throw new ArgumentException($"Plan target '{targetName}' has an unknown scope kind.", nameof(targetName));
        }

        if (string.Equals(rootScope.Kind.ToString(), kind.ToString(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(rootScope.Id, idPart, StringComparison.Ordinal))
        {
            return rootScope;
        }

        return new MemoryScope(kind, idPart, rootScope);
    }
}