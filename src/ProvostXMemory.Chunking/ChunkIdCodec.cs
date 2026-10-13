using System;
using System.Globalization;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Chunking;


/// <summary>
/// Canonical chunk-id codec: "<c>scopeKind|scopeId|recordId|chunkIndex</c>" (ordinal
/// pipe-separated). Embeds the parent's scope so a chunk id alone is enough to locate
/// its parent record in the store, and keeps record ids from colliding across scopes.
/// </summary>
public sealed class ChunkIdCodec
{
    private const char Separator = '|';

    /// <summary>Builds the canonical chunk id for a record's chunk at the given index.</summary>
    public string Compose(MemoryScope scope, string recordId, int chunkIndex)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentOutOfRangeException.ThrowIfNegative(chunkIndex);

        // The pipe is the codec separator: a component containing it produces an id
        // Parse can never decode (silent dense-recall loss). Reject loudly at compose time.
        if (scope.Id.Contains(Separator) || recordId.Contains(Separator))
        {
            throw new ArgumentException($"Scope id and record id must not contain '{Separator}'.");
        }


        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{scope.Kind}{Separator}{scope.Id}{Separator}{recordId}{Separator}{chunkIndex}");
    }

    /// <summary>Parses a canonical chunk id; returns null when the id is malformed.</summary>
    public ChunkIdParts? Parse(string chunkId)
    {
        if (string.IsNullOrWhiteSpace(chunkId))
        {
            return null;
        }

        var parts = chunkId.Split(Separator);
        if (parts.Length != 4
            || !Enum.TryParse<MemoryScopeKind>(parts[0], ignoreCase: false, out var kind)
            || !Enum.IsDefined(kind)
            || parts[1].Length == 0
            || parts[2].Length == 0
            || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            || parts[3].Length != index.ToString(CultureInfo.InvariantCulture).Length)
        {
            return null;
        }

        return new ChunkIdParts(kind, parts[1], parts[2], index);
    }
}

