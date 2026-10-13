using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Lexical;
using ProvostXMemory.VectorIndex;

namespace ProvostXMemory.RetrievalTests;

/// <summary>
/// Deterministic hash-based fake embedder (independent of the Consolidation test double):
/// each token hashes into a fixed dim slot via FNV-1a; the vector depends only on the text.
/// </summary>
public sealed class HashEmbedder : IVectorEmbedder
{
    private const int VectorDim = 128;

    public string ModelHash => "fake-hash-v1";

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

/// <summary>
/// In-memory, scope-isolated <see cref="IMemoryStore"/>: dictionaries keyed by the exact
/// (Kind, Id) pair; cross-scope reads are structurally impossible.
/// </summary>
public sealed class InMemoryStore : IMemoryStore
{
    private readonly Dictionary<string, List<MemoryRecord>> _scopes = new(StringComparer.Ordinal);

    private static string Key(string scopeKind, string scopeId) => $"{scopeKind}\x1f{scopeId}";

    public int RememberedMutations { get; private set; }

    /// <summary>All scopes that currently hold at least one record (test-side enumeration).</summary>

    public Task AddAsync(MemoryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        var records = RecordsOf(record.Scope.Kind.ToString(), record.Scope.Id);
        if (records.Any(existing => string.Equals(existing.Id, record.Id, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Duplicate record id in scope: {record.Id}");
        }

        records.Add(record);
        RememberedMutations++;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        var records = RecordsOf(record.Scope.Kind.ToString(), record.Scope.Id);
        var index = FindIndex(records, record.Id);
        if (index < 0)
        {
            throw new KeyNotFoundException($"No record '{record.Id}' in scope.");
        }

        records[index] = record;
        RememberedMutations++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string scopeKind, string scopeId, string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var records = RecordsOf(scopeKind, scopeId);
        var index = FindIndex(records, id);
        if (index >= 0)
        {
            records.RemoveAt(index);
        }

        RememberedMutations++;
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

/// <summary>
/// Index rig wiring one scope's artifacts: vector index (built from the store snapshot
/// with the facade's id conventions), lexical index, and record provider.
/// </summary>
public sealed class ScopeIndexRig
{
    private const string RecordAnchorPrefix = "record:";
    private const string ChunkPrefix = "chunk:";

    private readonly InMemoryStore _store;
    private readonly MemoryScope _scope;
    private readonly IVectorIndex _vectorIndex;
    private readonly ILexicalIndex _lexicalIndex;
    private readonly Chunking.ChunkIdCodec _codec = new();

    public ScopeIndexRig(InMemoryStore store, MemoryScope scope, IVectorEmbedder embedder)
    {
        _store = store;
        _scope = scope;
        _vectorIndex = new FlatJsonVectorIndex(
            new VectorNamespaceFile(
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"pvmx-retrieval-tests-{Guid.NewGuid():N}",
                    "index.json"),
                embedder.ModelHash,
                embedder.Dim),
            new VectorIndexGuard(embedder.ModelHash, embedder.Dim));
        _lexicalIndex = new LexicalIndex(new Bm25Scorer());
    }

    public IVectorIndex VectorIndex => _vectorIndex;

    public ILexicalIndex LexicalIndex => _lexicalIndex;

    public Chunking.ChunkIdCodec Codec => _codec;

    public int ChunkCountOf(MemoryRecord record)
    {
        var count = 0;
        foreach (var chunk in new Chunking.StructuredChunker().Split(record, new ChunkOptions()))
        {
            count++;
        }

        return count;
    }

    /// <summary>Builds/refreshes this scope's indexes from the store snapshot (chunk + anchor ids).</summary>
    public async Task RebuildAsync(IVectorEmbedder embedder)
    {
        var records = await _store.SnapshotAsync(_scope.Kind.ToString(), _scope.Id).ConfigureAwait(false);
        var items = new List<VectorItem>();
        foreach (var record in records)
        {
            var chunks = new Chunking.StructuredChunker().Split(record, new ChunkOptions());
            foreach (var chunk in chunks)
            {
                items.Add(new VectorItem(
                    $"{ChunkPrefix}{_codec.Compose(record.Scope, record.Id, chunk.Index)}",
                    await embedder.EmbedAsync(chunk.Content).ConfigureAwait(false)));
            }

            items.Add(new VectorItem(
                $"{RecordAnchorPrefix}{record.Id}",
                await embedder.EmbedAsync(record.Content).ConfigureAwait(false)));
        }

        await _vectorIndex.BuildAsync(items).ConfigureAwait(false);

        var documents = new List<LexicalDocument>(records.Count);
        foreach (var record in records)
        {
            var keyTerms = new List<string> { record.Key };
            keyTerms.AddRange(record.Tags);
            documents.Add(new LexicalDocument(record.Id, LexicalTokenizer.Tokenize(record.Content), keyTerms, record.Category));
        }

        _lexicalIndex.Build(documents);
    }
}