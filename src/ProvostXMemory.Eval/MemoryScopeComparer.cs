using System;
using System.Collections.Generic;

namespace ProvostXMemory.Eval;

/// <summary>Ordinal scope comparison helper (Kind + Id); shared by eval components.</summary>
public sealed class MemoryScopeComparer : IEqualityComparer<Abstractions.MemoryScope>
{
    /// <summary>Shared default instance (pure constant; no shared mutable state).</summary>
    public static readonly MemoryScopeComparer Ordinal = new();

    private MemoryScopeComparer()
    {
    }

    public bool Equals(Abstractions.MemoryScope? x, Abstractions.MemoryScope? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        return x is not null
            && y is not null
            && x.Kind == y.Kind
            && string.Equals(x.Id, y.Id, StringComparison.Ordinal);
    }

    public int GetHashCode(Abstractions.MemoryScope obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return HashCode.Combine(obj.Kind, StringComparer.Ordinal.GetHashCode(obj.Id));
    }
}