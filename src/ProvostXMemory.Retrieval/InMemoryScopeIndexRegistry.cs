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
/// In-memory scope → artifact registry for single-process hosts and test harnesses.
/// Keys are the exact (Kind, Id) pair; lookups never fall through to parent or child scopes.
/// </summary>
public sealed class InMemoryScopeIndexRegistry : IScopeIndexRegistry
{
    private readonly ConcurrentDictionary<ScopeKey, IVectorIndex> _vectorIndexes;
    private readonly ConcurrentDictionary<ScopeKey, ILexicalIndex> _lexicalIndexes;
    private readonly ConcurrentDictionary<ScopeKey, Func<MemoryScope, CancellationToken, Task<IReadOnlyList<MemoryRecord>>>> _recordProviders;

    public InMemoryScopeIndexRegistry()
    {
        _vectorIndexes = new ConcurrentDictionary<ScopeKey, IVectorIndex>();
        _lexicalIndexes = new ConcurrentDictionary<ScopeKey, ILexicalIndex>();
        _recordProviders = new ConcurrentDictionary<ScopeKey, Func<MemoryScope, CancellationToken, Task<IReadOnlyList<MemoryRecord>>>>();
    }

    /// <summary>Registers (or replaces) the vector index for one exact scope.</summary>
    public void RegisterVectorIndex(MemoryScope scope, IVectorIndex index)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(index);
        _vectorIndexes[ScopeKey.Of(scope)] = index;
    }

    /// <summary>Registers (or replaces) the lexical index for one exact scope.</summary>
    public void RegisterLexicalIndex(MemoryScope scope, ILexicalIndex index)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(index);
        _lexicalIndexes[ScopeKey.Of(scope)] = index;
    }

    /// <summary>Registers (or replaces) the record provider for one exact scope.</summary>
    public void RegisterRecordProvider(
        MemoryScope scope,
        Func<MemoryScope, CancellationToken, Task<IReadOnlyList<MemoryRecord>>> provider)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(provider);
        _recordProviders[ScopeKey.Of(scope)] = provider;
    }

    /// <inheritdoc />
    public IVectorIndex GetVectorIndex(MemoryScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (_vectorIndexes.TryGetValue(ScopeKey.Of(scope), out var index))
        {
            return index;
        }

        throw new KeyNotFoundException($"No vector index registered for scope '{scope.Kind}/{scope.Id}'.");
    }

    /// <inheritdoc />
    public ILexicalIndex GetLexicalIndex(MemoryScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (_lexicalIndexes.TryGetValue(ScopeKey.Of(scope), out var index))
        {
            return index;
        }

        throw new KeyNotFoundException($"No lexical index registered for scope '{scope.Kind}/{scope.Id}'.");
    }

    /// <inheritdoc />
    public Func<MemoryScope, CancellationToken, Task<IReadOnlyList<MemoryRecord>>> GetRecordProvider(MemoryScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (_recordProviders.TryGetValue(ScopeKey.Of(scope), out var provider))
        {
            return provider;
        }

        // Deterministic empty provider: scopes without a registered provider read nothing.
        return static (_, _) => Task.FromResult<IReadOnlyList<MemoryRecord>>(Array.Empty<MemoryRecord>());
    }

    /// <inheritdoc />
    public bool HasIndexes(MemoryScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var key = ScopeKey.Of(scope);
        return _vectorIndexes.ContainsKey(key) || _lexicalIndexes.ContainsKey(key);
    }

    /// <summary>Removes every registration (all scopes); used by full rebuilds.</summary>
    public void Reset()
    {
        _vectorIndexes.Clear();
        _lexicalIndexes.Clear();
        _recordProviders.Clear();
    }

    private readonly record struct ScopeKey(string Kind, string Id)
    {
        // Pure tuple factory; no shared mutable state.
        public static ScopeKey Of(MemoryScope scope) => new(scope.Kind.ToString(), scope.Id);
    }
}
