using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Chunking;
using ProvostXMemory.Storage;

using Xunit;

namespace ProvostXMemory.ChunkingTests;

/// <summary>ChunkParentResolver: malformed ids, wrong/foreign scopes, real store round-trip.</summary>
public sealed class ChunkParentResolverTests : IDisposable
{
    private readonly string _root;
    private readonly JsonlMemoryStore _store;
    private readonly ChunkParentResolver _resolver;

    public ChunkParentResolverTests()
    {
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pvx-parent-resolver", Guid.NewGuid().ToString("N"));
        _store = new JsonlMemoryStore(new MemoryRootLayout(_root, "model-x"));
        _resolver = new ChunkParentResolver(_store);
    }

    public void Dispose()
    {
        try
        {
            if (System.IO.Directory.Exists(_root))
            {
                System.IO.Directory.Delete(_root, recursive: true);
            }
        }
        catch (System.IO.IOException)
        {
        }
    }

    private static MemoryRecord Record(string scopeKind, string scopeId, string id, string content)
        => new(
            id,
            new MemoryScope(Enum.Parse<MemoryScopeKind>(scopeKind), scopeId),
            Key: $"{scopeId}:{id}",
            content);

    [Fact]
    public async Task Round_trip_through_real_store_resolve_chunk_to_parent()
    {
        var record = Record("Tenant", "acme", "doc-9", "# Title\nBody stored in jsonl.");
        await _store.AddAsync(record);

        var chunker = new StructuredChunker();
        var chunks = chunker.Split(record, new ChunkOptions(MaxChunkLength: 100));
        Assert.Equal(2, chunks.Count);

        foreach (var chunk in chunks)
        {
            var parent = await _resolver.ResolveAsync(chunk.ChunkId);
            Assert.NotNull(parent);
            Assert.Equal(record.Id, parent!.Id);
            Assert.Equal(record.Content, parent.Content);
            Assert.Equal(record.Scope.Kind, parent.Scope.Kind);
            Assert.Equal(record.Scope.Id, parent.Scope.Id);
        }
    }

    [Fact]
    public async Task Structured_and_size_chunker_ids_resolve_to_same_parent()
    {
        var record = Record("Session", "s1", "abc", "First part.\n\nSecond part.");
        await _store.AddAsync(record);

        var structuredIds = new StructuredChunker()
            .Split(record, new ChunkOptions(MaxChunkLength: 8))
            .Select(c => c.ChunkId)
            .ToList();
        var sizeIds = new SizeChunker()
            .Split(record, new ChunkOptions(MaxChunkLength: 8))
            .Select(c => c.ChunkId)
            .ToList();

        Assert.NotEmpty(structuredIds);
        Assert.Equal(sizeIds, structuredIds);
        foreach (var chunkId in structuredIds)
        {
            var parent = await _resolver.ResolveAsync(chunkId);
            Assert.NotNull(parent);
            Assert.Equal("abc", parent!.Id);
            Assert.Equal(record.Content, parent.Content);
        }
    }

    [Fact]
    public async Task Missing_parent_returns_null()
    {
        var record = Record("Tenant", "acme", "gone", "Some body text.");
        var chunkId = new SizeChunker().Split(record, new ChunkOptions())[0].ChunkId;

        // Record was never added to the store.
        Assert.Null(await _resolver.ResolveAsync(chunkId));
    }

    [Fact]
    public async Task Foreign_scope_record_does_not_leak_through_wrong_scope_id()
    {
        await _store.AddAsync(Record("Tenant", "acme", "secret-1", "cross-scope body"));
        var otherScopeChunk = Record("Tenant", "other", "secret-1", "other body");
        var chunkId = new SizeChunker().Split(otherScopeChunk, new ChunkOptions())[0].ChunkId;

        // Resolver reads Tenant/other, which has no records — structurally isolated.
        Assert.Null(await _resolver.ResolveAsync(chunkId));
    }

    [Fact]
    public async Task Wrong_kind_fails_the_lookup_too()
    {
        await _store.AddAsync(Record("Tenant", "acme", "k1", "typed body"));
        var sessionChunk = Record("Session", "acme", "k1", "typed body");
        var chunkId = new SizeChunker().Split(sessionChunk, new ChunkOptions())[0].ChunkId;
        Assert.Null(await _resolver.ResolveAsync(chunkId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage")]
    [InlineData("Tenant|acme|doc|notanumber")]
    [InlineData("Bogus|acme|doc|0")]
    [InlineData("Tenant|acme|doc|0|extra")]
    public async Task Malformed_ids_return_null(string chunkId)
    {
        Assert.Null(await _resolver.ResolveAsync(chunkId));
    }

    [Fact]
    public async Task Cancellation_is_honoured()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _resolver.ResolveAsync("Tenant|acme|r|0", cts.Token));
    }

    [Fact]
    public async Task Custom_codec_change_updates_id_format()
    {
        var custom = new ChunkIdCodec();
        var record = Record("Purpose", "p1", "rec", "content");
        await _store.AddAsync(record);
        var chunks = new SizeChunker(custom).Split(record, new ChunkOptions());
        var resolved = await _resolver.ResolveAsync(chunks[0].ChunkId);
        Assert.NotNull(resolved);
        Assert.Equal("rec", resolved!.Id);
    }
}