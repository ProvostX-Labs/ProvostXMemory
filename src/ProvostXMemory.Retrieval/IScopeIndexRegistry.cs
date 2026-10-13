using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Lexical;
using ProvostXMemory.VectorIndex;

namespace ProvostXMemory.Retrieval;


/// <summary>
/// Maps a <see cref="MemoryScope"/> to its per-scope retrieval artifacts. Implementations
/// must be scope-exact: a lookup for scope A never returns an artifact of scope B
/// (no parent fallthrough, no default forwarding) — cross-scope access stays
/// structurally impossible.
/// </summary>
public interface IScopeIndexRegistry
{
    /// <summary>Returns the scope-pinned vector index, or throws <see cref="KeyNotFoundException"/> when none is registered.</summary>
    IVectorIndex GetVectorIndex(MemoryScope scope);

    /// <summary>Returns the scope-pinned lexical index, or throws <see cref="KeyNotFoundException"/> when none is registered.</summary>
    ILexicalIndex GetLexicalIndex(MemoryScope scope);

    /// <summary>Returns the scope-pinned record snapshot provider (reads only the requested scope's records).</summary>
    Func<MemoryScope, CancellationToken, Task<IReadOnlyList<MemoryRecord>>> GetRecordProvider(MemoryScope scope);

    /// <summary>True when the scope has at least a vector or lexical index registered.</summary>
    bool HasIndexes(MemoryScope scope);
}

