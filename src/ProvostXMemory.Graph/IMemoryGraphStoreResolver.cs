using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Graph;

/// <summary>
/// Resolves the scope-pinned graph store for a memory scope. Implementations must
/// guarantee that the returned store is bound to exactly the requested scope, so
/// cross-scope access stays structurally impossible.
/// </summary>
public interface IMemoryGraphStoreResolver
{
    IMemoryGraphStore Resolve(MemoryScope scope);
}