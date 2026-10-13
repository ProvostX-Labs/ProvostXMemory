using System.Threading;
using System.Threading.Tasks;

namespace ProvostXMemory.Abstractions;

/// <summary>Default JSONL-backed store contract (transactional file I/O).</summary>
public interface IJsonlMemoryStore : IMemoryStore
{
    Task<MemoryEntryModel?> ReadEntryAsync(string scopeKind, string scopeId, string id, CancellationToken cancellationToken = default);

    Task WriteEntryAsync(string scopeKind, string scopeId, MemoryEntryModel entry, CancellationToken cancellationToken = default);
}