using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.Abstractions;

using Xunit;

namespace ProvostXMemory.SystemTests.Unit;

/// <summary>
/// MemoryScope hierarchy semantics at type level: chain flattening, parent containment,
/// WithParent rewrites, kind equality, and scope-qualified path segments. Behavioral
/// depth where earlier suites only touch scopes incidentally. Pure types — no storage.
/// </summary>
public sealed class MemoryScopeSemanticsTests
{
    [Fact]
    public void Chain_flattens_outermost_first_and_includes_self()
    {
        var tenant = new MemoryScope(MemoryScopeKind.Tenant, "acme");
        var session = new MemoryScope(MemoryScopeKind.Session, "chat-1", tenant);
        var purpose = new MemoryScope(MemoryScopeKind.Purpose, "research", session);

        var chain = purpose.Chain();

        Assert.Equal(3, chain.Count);
        Assert.Equal(tenant, chain[0]);
        Assert.Equal(session, chain[1]);
        Assert.Equal(purpose, chain[2]);
    }

    [Fact]
    public void Root_scope_chain_is_single_element()
    {
        var root = new MemoryScope(MemoryScopeKind.Platform, "root");

        var chain = root.Chain();

        Assert.Single(chain);
        Assert.Same(root, chain[0]);
    }

    [Fact]
    public void Contains_is_true_for_self_and_all_descendants_only()
    {
        var tenant = new MemoryScope(MemoryScopeKind.Tenant, "acme");
        var session = new MemoryScope(MemoryScopeKind.Session, "chat-1", tenant);
        var purpose = new MemoryScope(MemoryScopeKind.Purpose, "research", session);
        var stranger = new MemoryScope(MemoryScopeKind.Tenant, "other");

        Assert.True(tenant.Contains(tenant));
        Assert.True(tenant.Contains(session));
        Assert.True(tenant.Contains(purpose));
        Assert.False(session.Contains(tenant)); // parents are NOT contained
        Assert.False(tenant.Contains(stranger));
        Assert.False(stranger.Contains(purpose));
    }

    [Fact]
    public void Contains_distinguishes_same_id_different_kind()
    {
        var platform = new MemoryScope(MemoryScopeKind.Platform, "alpha");
        var custom = new MemoryScope(MemoryScopeKind.Custom, "alpha");

        Assert.False(platform.Contains(custom));
        Assert.False(custom.Contains(platform));
        Assert.NotEqual(platform, custom);
    }

    [Fact]
    public void WithParent_replaces_the_parent_and_preserves_identity()
    {
        var originalParent = new MemoryScope(MemoryScopeKind.Tenant, "old");
        var newParent = new MemoryScope(MemoryScopeKind.Tenant, "new");
        var child = new MemoryScope(MemoryScopeKind.Session, "s1", originalParent);

        var reParented = child.WithParent(newParent);

        Assert.Equal(newParent, reParented.ParentScope);
        Assert.Equal(child.Kind, reParented.Kind);
        Assert.Equal(child.Id, reParented.Id);
        Assert.Equal(originalParent, child.ParentScope); // immutable original untouched
    }

    [Fact]
    public void PathSegment_maps_kind_and_id_and_parent_is_not_encoded()
    {
        var parent = new MemoryScope(MemoryScopeKind.Tenant, "acme");
        var child = new MemoryScope(MemoryScopeKind.Session, "s1", parent);

        Assert.Equal("Tenant/acme", parent.PathSegment);
        Assert.Equal("Session/s1", child.PathSegment); // hierarchical, yet segment is local
    }

    [Fact]
    public void Equality_is_kind_plus_id_regardless_of_parent_subtree()
    {
        var parentA = new MemoryScope(MemoryScopeKind.Tenant, "acme");
        var parentB = new MemoryScope(MemoryScopeKind.Tenant, "globex");
        var left = new MemoryScope(MemoryScopeKind.Session, "s1", parentA);
        var right = new MemoryScope(MemoryScopeKind.Session, "s1", parentB);

        // Records are value-equal on all members — parent differs, so NOT equal.
        Assert.NotEqual(left, right);
        Assert.Equal(new MemoryScope(MemoryScopeKind.Session, "s1", parentA), left);
    }

    [Fact]
    public void All_five_kinds_round_trip_through_chain_and_path()
    {
        foreach (var kind in Enum.GetValues<MemoryScopeKind>())
        {
            var scope = new MemoryScope(kind, $"id-{kind}");
            Assert.Equal(kind, scope.Chain()[0].Kind);
            Assert.Equal($"{kind}/id-{kind}", scope.PathSegment);
        }
    }

    [Fact]
    public void Deeply_nested_chain_preserves_order_and_contains_transitively()
    {
        var current = new MemoryScope(MemoryScopeKind.Platform, "p0");
        var root = current;
        for (var depth = 1; depth < 6; depth++)
        {
            current = new MemoryScope(MemoryScopeKind.Custom, $"n{depth}", current);
        }

        var chain = current.Chain();
        Assert.Equal(6, chain.Count);
        Assert.Equal("p0", chain[0].Id);
        Assert.Equal("n5", chain[5].Id);
        Assert.True(root.Contains(current));
        Assert.True(current.Contains(current));
        Assert.False(current.Contains(root));
    }

    [Fact]
    public void Contains_throws_on_null_other()
    {
        var scope = new MemoryScope(MemoryScopeKind.Session, "s1");

        Assert.Throws<ArgumentNullException>(() => scope.Contains(null!));
    }
}