using System;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Lexical;
using ProvostXMemory.Retrieval;
using ProvostXMemory.VectorIndex;

using Xunit;

namespace ProvostXMemory.RetrievalTests;

/// <summary>InMemoryScopeIndexRegistry: scope-exact registration, lookup, miss behavior.</summary>
public sealed class ScopeIndexRegistryTests
{
    private static readonly MemoryScope ScopeA = new(MemoryScopeKind.Session, "a");
    private static readonly MemoryScope ScopeB = new(MemoryScopeKind.Session, "b");
    private static readonly MemoryScope ScopeC = new(MemoryScopeKind.Tenant, "a");

    [Fact]
    public void Lookup_returns_the_exact_registered_artifact()
    {
        var registry = new InMemoryScopeIndexRegistry();
        var vector = new StubVectorIndex();
        var lexical = new StubLexicalIndex();
        registry.RegisterVectorIndex(ScopeA, vector);
        registry.RegisterLexicalIndex(ScopeA, lexical);

        Assert.Same(vector, registry.GetVectorIndex(ScopeA));
        Assert.Same(lexical, registry.GetLexicalIndex(ScopeA));
        Assert.True(registry.HasIndexes(ScopeA));
    }

    [Fact]
    public void Same_id_other_kind_is_a_different_scope()
    {
        var registry = new InMemoryScopeIndexRegistry();
        registry.RegisterVectorIndex(ScopeA, new StubVectorIndex());

        // Session/a registered; Tenant/a is a different scope and must not resolve.
        Assert.Throws<KeyNotFoundException>(() => registry.GetVectorIndex(ScopeC));
        Assert.False(registry.HasIndexes(ScopeC));
    }

    [Fact]
    public void Missing_registrations_throw()
    {
        var registry = new InMemoryScopeIndexRegistry();
        Assert.Throws<KeyNotFoundException>(() => registry.GetVectorIndex(ScopeB));
        Assert.Throws<KeyNotFoundException>(() => registry.GetLexicalIndex(ScopeB));
        Assert.False(registry.HasIndexes(ScopeB));
    }

    [Fact]
    public async Task Missing_record_provider_yields_empty_provider()
    {
        var registry = new InMemoryScopeIndexRegistry();
        var provider = registry.GetRecordProvider(ScopeB);
        var snapshot = await provider(ScopeB, default);
        Assert.Empty(snapshot);
    }

    [Fact]
    public async Task Registered_provider_is_used()
    {
        var registry = new InMemoryScopeIndexRegistry();
        var expected = new[] { new MemoryRecord("r1", ScopeA, "k", "content") };
        registry.RegisterRecordProvider(ScopeA, (_, _) => Task.FromResult<IReadOnlyList<MemoryRecord>>(expected));

        var provider = registry.GetRecordProvider(ScopeA);
        Assert.Same(expected[0], (await provider(ScopeA, default))[0]);
    }

    [Fact]
    public void Reset_clears_everything()
    {
        var registry = new InMemoryScopeIndexRegistry();
        registry.RegisterVectorIndex(ScopeA, new StubVectorIndex());
        registry.RegisterLexicalIndex(ScopeA, new StubLexicalIndex());
        registry.Reset();

        Assert.False(registry.HasIndexes(ScopeA));
        Assert.Throws<KeyNotFoundException>(() => registry.GetVectorIndex(ScopeA));
    }

    private sealed class StubVectorIndex : IVectorIndex
    {
        public int Dim => 4;

        public long Count => 0;

        public Task BuildAsync(IReadOnlyList<VectorItem> items, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddAsync(VectorItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ScoredVectorId>> SearchAsync(IReadOnlyList<float> vector, int k, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ScoredVectorId>>(Array.Empty<ScoredVectorId>());

        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubLexicalIndex : ILexicalIndex
    {
        public long Count => 0;

        public CorpusStatistics Statistics => CorpusStatistics.Empty;

        public void Build(IReadOnlyList<LexicalDocument> documents)
        {
        }

        public void IncrementalAdd(LexicalDocument document)
        {
        }

        public bool Remove(string documentId) => false;

        public IReadOnlyList<LexicalDocument> EnumerateDocuments() => Array.Empty<LexicalDocument>();

        public IReadOnlyList<ScoredLexicalId> Search(IReadOnlyList<string> queryTokens, int topK) => Array.Empty<ScoredLexicalId>();
    }
}