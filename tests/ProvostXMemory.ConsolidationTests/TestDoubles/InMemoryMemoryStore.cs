using ProvostXMemory.Abstractions;

namespace ProvostXMemory.ConsolidationTests;

/// <summary>
/// In-memory <see cref="IMemoryStore"/> test double. Scope-isolated dictionaries; no
/// transactional file I/O — consolidation must only talk to the interface anyway.
/// </summary>
public sealed class InMemoryMemoryStore : IMemoryStore
{
    private readonly Dictionary<string, List<MemoryRecord>> _scopes = new(StringComparer.Ordinal);

    private static string ScopeKey(string scopeKind, string scopeId) => $"{scopeKind}\x1f{scopeId}";

    public Task AddAsync(MemoryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var records = RecordsOf(record.Scope.Kind.ToString(), record.Scope.Id);
        if (records.Any(existing => string.Equals(existing.Id, record.Id, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("A record with the same id already exists in this scope.");
        }

        records.Add(record);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var records = RecordsOf(record.Scope.Kind.ToString(), record.Scope.Id);
        var index = FindIndex(records, record.Id);
        if (index < 0)
        {
            throw new KeyNotFoundException("No record with the given id exists in this scope.");
        }

        records[index] = record;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string scopeKind, string scopeId, string id, CancellationToken cancellationToken = default)
    {
        var records = RecordsOf(scopeKind, scopeId);
        var index = FindIndex(records, id);
        if (index >= 0)
        {
            records.RemoveAt(index);
        }

        return Task.CompletedTask;
    }

    public Task<MemoryRecord?> GetAsync(string scopeKind, string scopeId, string id, CancellationToken cancellationToken = default)
    {
        var records = RecordsOf(scopeKind, scopeId);
        var index = FindIndex(records, id);
        return Task.FromResult(index < 0 ? null : records[index]);
    }

    public Task<IReadOnlyList<MemoryRecord>> ListAsync(string scopeKind, string scopeId, CancellationToken cancellationToken = default)
        => Task.FromResult((IReadOnlyList<MemoryRecord>)RecordsOf(scopeKind, scopeId).ToList());

    public Task<long> CountAsync(string scopeKind, string scopeId, CancellationToken cancellationToken = default)
        => Task.FromResult((long)RecordsOf(scopeKind, scopeId).Count);

    public Task<IReadOnlyList<MemoryRecord>> SnapshotAsync(string scopeKind, string scopeId, CancellationToken cancellationToken = default)
        => ListAsync(scopeKind, scopeId, cancellationToken);

    private List<MemoryRecord> RecordsOf(string scopeKind, string scopeId)
    {
        var key = ScopeKey(scopeKind, scopeId);
        if (!_scopes.TryGetValue(key, out var records))
        {
            records = new List<MemoryRecord>();
            _scopes[key] = records;
        }

        return records;
    }

    private static int FindIndex(List<MemoryRecord> records, string id)
    {
        for (var i = 0; i < records.Count; i++)
        {
            if (string.Equals(records[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}