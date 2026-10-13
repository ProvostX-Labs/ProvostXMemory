using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProvostXMemory.Abstractions;

/// <summary>
/// <para>The durable record store seam. Every method takes the scope explicitly
/// (kind + id) — there is no scope-less operation anywhere in the contract, so a
/// store implementation physically cannot conflate scopes. The default
/// <c>JsonlMemoryStore</c> lays out one <c>records.jsonl</c> per scope directory with
/// transactional rewrites (temp file + atomic move) and tolerant reads (blank lines and
/// legacy entries without Reps/Meta deserialize to defaults).</para>
/// <para><b>Concurrency contract:</b> implementations are not required to be internally
/// thread-safe — <c>IMemoryService</c> serializes store mutations per service
/// instance; direct multi-threaded store access is the host's own synchronization problem.</para>
/// </summary>
public interface IMemoryStore
{
    Task AddAsync(MemoryRecord record, CancellationToken cancellationToken = default);

    Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken = default);

    Task DeleteAsync(string scopeKind, string scopeId, string id, CancellationToken cancellationToken = default);

    Task<MemoryRecord?> GetAsync(string scopeKind, string scopeId, string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryRecord>> ListAsync(string scopeKind, string scopeId, CancellationToken cancellationToken = default);

    Task<long> CountAsync(string scopeKind, string scopeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryRecord>> SnapshotAsync(string scopeKind, string scopeId, CancellationToken cancellationToken = default);
}