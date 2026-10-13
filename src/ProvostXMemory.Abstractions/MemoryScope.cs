using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProvostXMemory.Abstractions;


/// <summary>
/// <para>One isolation boundary of memory: the (Kind, Id) pair every record, index, and
/// log line belongs to. Records written to one scope are structurally invisible to
/// operations on another — the storage root layout pins each scope to its own directory
/// tree and the index registry keys indexes by the exact scope pair.</para>
/// <para>Scopes may carry a <see cref="ParentScope"/> chain for hierarchical fan-out
/// (query plans target a root and resolve child scopes), but lookups never fall through
/// to parents: a query against scope A sees scope A, nothing else, whatever its ancestry.</para>
/// </summary>
/// <remarks>
/// Typical kinds: <c>MemoryScopeKind.Platform</c> (per surface),
/// <c>MemoryScopeKind.Session</c> (per conversation),
/// <c>MemoryScopeKind.Tenant</c> (per customer in multi-tenant hosts),
/// <c>MemoryScopeKind.Purpose</c> (per knowledge base),
/// <c>MemoryScopeKind.Custom</c> (anything else). Scope ids must be safe path
/// segments (letters, digits, '-', '_'); traversal sequences are rejected at the layout layer.
/// </remarks>
public sealed record MemoryScope(
    MemoryScopeKind Kind,
    string Id,
    MemoryScope? ParentScope = null)
{
    /// <summary>Scope path segment ("<see cref="Kind"/>/<see cref="Id"/>").</summary>
    public string PathSegment => $"{Kind}/{Id}";

    /// <summary>Flattens the scope chain, outermost first.</summary>
    public IReadOnlyList<MemoryScope> Chain()
    {
        var chain = new List<MemoryScope>();
        var current = this;
        while (current is not null)
        {
            chain.Add(current);
            current = current.ParentScope;
        }

        chain.Reverse();
        return chain;
    }

    /// <summary>Returns true when <paramref name="other"/> is this scope or a descendant of it.</summary>
    public bool Contains(MemoryScope other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.Chain().Contains(this);
    }

    /// <summary>Returns this scope with <paramref name="parent"/> as its parent.</summary>
    public MemoryScope WithParent(MemoryScope parent) => this with { ParentScope = parent };
}

