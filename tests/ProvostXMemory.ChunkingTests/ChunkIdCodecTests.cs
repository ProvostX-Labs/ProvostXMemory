using ProvostXMemory.Chunking;

using Xunit;

namespace ProvostXMemory.ChunkingTests;

/// <summary>
/// Canonical chunk-id codec composition/parsing: scope-bound ids, malformed input
/// rejection, index bounds and ordinal round-trips.
/// </summary>
public sealed class ChunkIdCodecTests
{
    private readonly ChunkIdCodec _codec = new();

    [Fact]
    public void Compose_then_Parse_round_trips_scope_record_and_index()
    {
        var scope = new Abstractions.MemoryScope(Abstractions.MemoryScopeKind.Tenant, "acme");
        var id = _codec.Compose(scope, "rec-1", 7);

        var parts = _codec.Parse(id);

        Assert.NotNull(parts);
        Assert.Equal(Abstractions.MemoryScopeKind.Tenant, parts!.ScopeKind);
        Assert.Equal("acme", parts.ScopeId);
        Assert.Equal("rec-1", parts.RecordId);
        Assert.Equal(7, parts.ChunkIndex);
    }

    [Fact]
    public void Ids_embed_scope_so_same_record_id_in_other_scope_does_not_collide()
    {
        var tenant = new Abstractions.MemoryScope(Abstractions.MemoryScopeKind.Tenant, "t1");
        var session = new Abstractions.MemoryScope(Abstractions.MemoryScopeKind.Session, "s1");

        var a = _codec.Compose(tenant, "same", 0);
        var b = _codec.Compose(session, "same", 0);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Parse_rejects_malformed_ids()
    {
        Assert.Null(_codec.Parse(""));
        Assert.Null(_codec.Parse("   "));
        Assert.Null(_codec.Parse("noseparator"));
        Assert.Null(_codec.Parse("Tenant|s|r"));              // missing index
        Assert.Null(_codec.Parse("Tenant|s|r|x|extra"));      // too many parts
        Assert.Null(_codec.Parse("Tenant|s|r|-1"));           // negative index
        Assert.Null(_codec.Parse("Tenant|s|r|+2"));           // sign not allowed
        Assert.Null(_codec.Parse("Tenant|s|r|02"));           // leading zero
        Assert.Null(_codec.Parse("NotAKind|s|r|0"));          // unknown scope kind
        Assert.Null(_codec.Parse("tenant|s|r|0"));            // kind parses case-insensitively? No: exact names only
    }

    [Fact]
    public void Compose_rejects_negative_index()
    {
        var scope = new Abstractions.MemoryScope(Abstractions.MemoryScopeKind.Session, "s");
        Assert.Throws<ArgumentOutOfRangeException>(() => _codec.Compose(scope, "r", -1));
    }

    [Fact]
    public void Parse_rejects_id_with_unknown_kind_even_when_index_is_valid()
    {
        Assert.Null(_codec.Parse("Platform|p1|r9|3|."));
    }
    /// <summary>
    /// Regression pin (2026-10-14): components containing the pipe separator produce ids
    /// Parse can never decode (silent dense-recall loss) — Compose must reject loudly.
    /// </summary>
    [Fact]
    public void Compose_RejectsPipeInScopeOrRecordId()
    {
        var scope = new Abstractions.MemoryScope(Abstractions.MemoryScopeKind.Session, "s|1");

        Assert.Throws<ArgumentException>(() => _codec.Compose(scope, "rec", 0));
        Assert.Throws<ArgumentException>(() => _codec.Compose(new Abstractions.MemoryScope(Abstractions.MemoryScopeKind.Session, "s1"), "re|c", 0));
    }
}
