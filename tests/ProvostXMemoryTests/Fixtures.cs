using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.VectorIndex;

using Xunit;

namespace ProvostXMemoryTests;

/// <summary>
/// Scope-isolated in-memory <see cref="IMemoryStore"/> for facade tests, plus
/// <see cref="ProvostXMemory.IScopesAware"/> so Scopes() enumeration works.
/// </summary>
public sealed class FacadeStore : IMemoryStore, ProvostXMemory.IScopesAware
{
    private readonly Dictionary<string, List<MemoryRecord>> _scopes = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    private static string Key(string scopeKind, string scopeId) => $"{scopeKind}\x1f{scopeId}";

    public Task AddAsync(MemoryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            var records = RecordsOf(record.Scope.Kind.ToString(), record.Scope.Id);
            if (records.Any(existing => string.Equals(existing.Id, record.Id, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Duplicate record id in scope: {record.Id}");
            }

            records.Add(record);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var records = RecordsOf(record.Scope.Kind.ToString(), record.Scope.Id);
        var index = FindIndex(records, record.Id);
        if (index < 0)
        {
            throw new KeyNotFoundException($"No record '{record.Id}' in scope.");
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

    public IReadOnlyList<MemoryScope> Scopes()
    {
        var scopes = new List<MemoryScope>();
        foreach (var pair in _scopes)
        {
            var parts = pair.Key.Split('\x1f');
            if (Enum.TryParse<MemoryScopeKind>(parts[0], ignoreCase: true, out var kind))
            {
                scopes.Add(new MemoryScope(kind, parts[1]));
            }
        }

        return scopes;
    }

    private List<MemoryRecord> RecordsOf(string scopeKind, string scopeId)
    {
        var key = Key(scopeKind, scopeId);
        if (!_scopes.TryGetValue(key, out var records))
        {
            records = new List<MemoryRecord>();
            _scopes[key] = records;
        }

        return records;
    }

    // Pure id lookup; intentional static per architecture rule.
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

/// <summary>Deterministic hash embedder for facade tests (tokens hash into fixed slots).</summary>
public sealed class HashEmbedder : IVectorEmbedder
{
    private const int VectorDim = 128;

    public string ModelHash => "model-hash-v1";

    public int Dim => VectorDim;

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var vector = new float[VectorDim];
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var isWord = i < text.Length && char.IsLetterOrDigit(text[i]);
            if (isWord && start < 0)
            {
                start = i;
            }
            else if (!isWord && start >= 0)
            {
                vector[Slot(text[start..i].ToLowerInvariant())] += 1f;
                start = -1;
            }
        }

        return Task.FromResult(vector);
    }

    // Pure hash → slot mapping (FNV-1a 64, mod dim); intentional static per architecture rule.
    private static int Slot(string token)
    {
        var hash = 0xcbf29ce484222325UL;
        foreach (var ch in token)
        {
            hash ^= ch;
            hash = hash * 0x100000001b3UL;
        }

        return (int)(hash % VectorDim);
    }
}