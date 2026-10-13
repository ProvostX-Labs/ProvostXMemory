using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Chunking;

/// <summary>
/// Resolves a canonical chunk id to its parent record via an <see cref="IMemoryStore"/>
/// snapshot of the scope embedded in the id. A malformed id or a missing parent record
/// yields null; the chunk index itself is never validated against a live chunk list.
/// </summary>
public sealed class ChunkParentResolver : IParentResolver
{
    private readonly IMemoryStore _store;
    private readonly ChunkIdCodec _codec;

    public ChunkParentResolver(IMemoryStore store, ChunkIdCodec? codec = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _codec = codec ?? new ChunkIdCodec();
    }

    /// <inheritdoc />
    public async Task<MemoryRecord?> ResolveAsync(string chunkId, CancellationToken cancellationToken = default)
    {
        if (_codec.Parse(chunkId) is not { } parts)
        {
            return null;
        }

        var snapshot = await _store
            .SnapshotAsync(parts.ScopeKind.ToString(), parts.ScopeId, cancellationToken)
            .ConfigureAwait(false);

        return snapshot.FirstOrDefault(record => string.Equals(record.Id, parts.RecordId, StringComparison.Ordinal));
    }
}