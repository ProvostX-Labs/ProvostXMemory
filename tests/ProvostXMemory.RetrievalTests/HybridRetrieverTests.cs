using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Fusion;
using ProvostXMemory.VectorIndex;
using ProvostXMemory.Retrieval;
using ProvostXMemory.Telemetry;

using Xunit;

namespace ProvostXMemory.RetrievalTests;

/// <summary>HybridRetriever: parallel dense+lexical fan-in, RRF fusion, per-scope isolation, diagnostics.</summary>
public sealed class HybridRetrieverTests
{
    private static (InMemoryStore Store, MemoryScope Scope, HashEmbedder Embedder, ScopeIndexRig Rig) Arrange(
        string scopeId = "s1",
        params MemoryRecord[] records)
    {
        var store = new InMemoryStore();
        var scope = new MemoryScope(MemoryScopeKind.Session, scopeId);
        var embedder = new HashEmbedder();
        var rig = new ScopeIndexRig(store, scope, embedder);
        foreach (var record in records)
        {
            store.AddAsync(record).GetAwaiter().GetResult();
        }

        rig.RebuildAsync(embedder).GetAwaiter().GetResult();
        return (store, scope, embedder, rig);
    }

    private static HybridRetriever CreateRetriever(InMemoryStore store, MemoryScope scope, HashEmbedder embedder, ScopeIndexRig rig, bool withReranker = false)
    {
        var registry = new InMemoryScopeIndexRegistry();
        registry.RegisterVectorIndex(scope, rig.VectorIndex);
        registry.RegisterLexicalIndex(scope, rig.LexicalIndex);
        registry.RegisterRecordProvider(
            scope,
            (targetScope, token) => targetScope.Kind == scope.Kind && targetScope.Id == scope.Id
                ? store.SnapshotAsync(scope.Kind.ToString(), scope.Id, token)
                : Task.FromResult<IReadOnlyList<MemoryRecord>>(Array.Empty<MemoryRecord>()));
        return new HybridRetriever(
            registry,
            embedder,
            new ReciprocalRankFuser(),
            new InMemoryMemoryTelemetry(),
            withReranker ? new Reranking.ScoreBoostReranker() : null);
    }

    private static MemoryRecord Rec(string id, string content, string[]? tags = null)
        => new(
            Id: id,
            Scope: new MemoryScope(MemoryScopeKind.Session, "s1"),
            Key: id,
            Content: content)
        {
            Tags = tags ?? Array.Empty<string>(),
        };

    private static MemoryRecord RecordFor(MemoryScope scope, string id, string content, string[]? tags = null)
        => new(
            Id: id,
            Scope: scope,
            Key: id,
            Content: content)
        {
            Tags = tags ?? Array.Empty<string>(),
        };

    [Fact]
    public async Task Returns_matching_record_by_content()
    {
        var (store, scope, embedder, rig) = Arrange(
            "s1",
            Rec("kubernetes-notes", "kubernetes cluster deployment rollout notes"),
            Rec("weather-report", "sunny weather with light breeze tomorrow"));

        var retriever = CreateRetriever(store, scope, embedder, rig);
        var result = await retriever.RetrieveAsync(new RetrievalQuery(
            Text: "kubernetes deployment rollout",
            Scope: scope,
            TopK: 3));

        Assert.NotEmpty(result.Records);
        Assert.Equal("kubernetes-notes", result.Records[0].Record.Id);
    }

    [Fact]
    public async Task Fusion_combines_dense_and_lexical_evidence()
    {
        var (store, scope, embedder, rig) = Arrange(
            "s1",
            Rec("alpha", "alpha bravo charlie delta echo"),
            Rec("bravo-only", "bravo bravo bravo unrelated vocabulary"),
            Rec("zulu", "completely different zulu yankee delta"));

        var retriever = CreateRetriever(store, scope, embedder, rig);
        var result = await retriever.RetrieveAsync(new RetrievalQuery(
            Text: "alpha bravo",
            Scope: scope,
            TopK: 3));

        Assert.Equal(2, result.Diagnostics.Count(stage => stage.Stage == "dense" || stage.Stage == "lexical"));
        var fusion = result.Diagnostics.Single(stage => stage.Stage == "fusion");
        Assert.True(fusion.Scores["hits"] >= 2, $"expected at least 2 fused hits, got {fusion.Scores["hits"]}");
        // The record matching both modalities outranks the single-modality hit.
        Assert.Equal("alpha", result.Records[0].Record.Id);
    }

    [Fact]
    public async Task Scope_isolation_between_two_rigs()
    {
        var storeA = new InMemoryStore();
        var scopeA = new MemoryScope(MemoryScopeKind.Session, "scope-a");
        var embedder = new HashEmbedder();
        var recordA = RecordFor(scopeA, "secret-a", "confidential tenant alpha document");
        await storeA.AddAsync(recordA);
        var rigA = new ScopeIndexRig(storeA, scopeA, embedder);
        await rigA.RebuildAsync(embedder);

        var storeB = new InMemoryStore();
        var scopeB = new MemoryScope(MemoryScopeKind.Session, "scope-b");
        await storeB.AddAsync(RecordFor(scopeB, "public-b", "public tenant beta document"));
        var rigB = new ScopeIndexRig(storeB, scopeB, embedder);
        await rigB.RebuildAsync(embedder);

        var registry = new InMemoryScopeIndexRegistry();
        registry.RegisterVectorIndex(scopeA, rigA.VectorIndex);
        registry.RegisterLexicalIndex(scopeA, rigA.LexicalIndex);
        registry.RegisterRecordProvider(scopeA, (_, _) => storeA.SnapshotAsync("Session", "scope-a"));
        registry.RegisterVectorIndex(scopeB, rigB.VectorIndex);
        registry.RegisterLexicalIndex(scopeB, rigB.LexicalIndex);
        registry.RegisterRecordProvider(scopeB, (_, _) => storeB.SnapshotAsync("Session", "scope-b"));

        var retriever = new HybridRetriever(registry, embedder, new ReciprocalRankFuser());
        var resultA = await retriever.RetrieveAsync(new RetrievalQuery("confidential alpha", scopeA, 5));
        var resultB = await retriever.RetrieveAsync(new RetrievalQuery("confidential alpha", scopeB, 5));

        // Scope A finds its record; scope B sees nothing of it. NOTE: resultB may
        // contain scope-B's OWN records — a dense ANN search returns its nearest
        // neighbors regardless of absolute similarity, which is a similarity-quality
        // concern, NOT a scope leak. The isolation guarantee is: no scope-A record
        // ever appears in a scope-B result.
        Assert.Contains(resultA.Records, hit => hit.Record.Id == "secret-a");
        Assert.DoesNotContain(resultB.Records, hit => hit.Record.Id == "secret-a");
        Assert.All(resultB.Records, hit => Assert.Equal("scope-b", hit.Record.Scope.Id));
    }

    [Fact]
    public async Task Empty_query_yields_empty_result()
    {
        var (store, scope, embedder, rig) = Arrange("s1", Rec("alpha", "alpha bravo"));

        var retriever = CreateRetriever(store, scope, embedder, rig);
        var result = await retriever.RetrieveAsync(new RetrievalQuery("", scope, 5));

        Assert.Empty(result.Records);
    }

    [Fact]
    public async Task Invalid_topK_throws()
    {
        var (store, scope, embedder, rig) = Arrange("s1", Rec("alpha", "alpha bravo"));

        var retriever = CreateRetriever(store, scope, embedder, rig);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => retriever.RetrieveAsync(new RetrievalQuery("alpha", scope, 0)));
    }

    [Fact]
    public async Task Reranker_stage_changes_score_provenance()
    {
        var (store, scope, embedder, rig) = Arrange(
            "s1",
            Rec("tagged", "kubernetes rollout details", tags: new[] { "kubernetes" }),
            Rec("plain", "kubernetes rollout details"));

        var retriever = CreateRetriever(store, scope, embedder, rig, withReranker: true);
        var result = await retriever.RetrieveAsync(new RetrievalQuery("kubernetes", scope, 5));

        var tagged = result.Records.First(hit => hit.Record.Id == "tagged");
        Assert.Contains("rerank.score-boost", tagged.Provenance.Retriever, StringComparison.Ordinal);
        Assert.True(tagged.Score > result.Records.First(hit => hit.Record.Id == "plain").Score);
    }

    /// <summary>
    /// Regression pin (2026-10-14): dense-stage index ids are prefixed ("record:&lt;id&gt;",
    /// "chunk:&lt;codec-id&gt;") and MUST resolve to their parent record at the resolve
    /// stage. Before the fix, prefixed ids silently failed lookup and dense-only hits
    /// were dropped, making recall effectively lexical-only.
    /// </summary>
    [Fact]
    public async Task DenseOnlyPrefixedIds_ResolveToParentRecords()
    {
        var store = new InMemoryStore();
        var scope = new MemoryScope(MemoryScopeKind.Session, "s1");
        var embedder = new HashEmbedder();
        var record = Rec("dense-only-target", "completely unrelated prose body");
        await store.AddAsync(record);

        var codec = new Chunking.ChunkIdCodec();
        var chunkId = $"chunk:{codec.Compose(scope, record.Id, 0)}";
        var denseIndex = new FixedDenseIndex(embedder, "record:" + record.Id, chunkId);
        var lexicalIndex = new ProvostXMemory.Lexical.LexicalIndex(new ProvostXMemory.Lexical.Bm25Scorer());
        lexicalIndex.Build(Array.Empty<ProvostXMemory.Lexical.LexicalDocument>());

        var retriever = CreateRetrieverWithIndexes(store, scope, embedder, denseIndex, lexicalIndex);
        var result = await retriever.RetrieveAsync(new RetrievalQuery("zzzqqq", scope), CancellationToken.None);

        Assert.Contains(result.Records, hit => hit.Record.Id == "dense-only-target");
        Assert.Equal(2, result.Records.Count); // both the record anchor and its chunk resolve to the same record
    }

    [Fact]
    public async Task ChunkIdFromForeignScope_DoesNotResolve()
    {
        var store = new InMemoryStore();
        var scope = new MemoryScope(MemoryScopeKind.Session, "s1");
        var embedder = new HashEmbedder();
        var record = Rec("s1-record", "some body text");
        await store.AddAsync(record);

        var codec = new Chunking.ChunkIdCodec();
        var foreignChunkId = $"chunk:{codec.Compose(new MemoryScope(MemoryScopeKind.Session, "other"), record.Id, 0)}";
        var denseIndex = new FixedDenseIndex(embedder, foreignChunkId);
        var lexicalIndex = new ProvostXMemory.Lexical.LexicalIndex(new ProvostXMemory.Lexical.Bm25Scorer());
        lexicalIndex.Build(Array.Empty<ProvostXMemory.Lexical.LexicalDocument>());

        var retriever = CreateRetrieverWithIndexes(store, scope, embedder, denseIndex, lexicalIndex);
        var result = await retriever.RetrieveAsync(new RetrievalQuery("zzzqqq", scope), CancellationToken.None);

        Assert.Empty(result.Records); // foreign-scope chunk id must never resolve into this scope
    }

    private static HybridRetriever CreateRetrieverWithIndexes(
        InMemoryStore store,
        MemoryScope scope,
        HashEmbedder embedder,
        IVectorIndex denseIndex,
        ProvostXMemory.Lexical.ILexicalIndex lexicalIndex)
    {
        var registry = new InMemoryScopeIndexRegistry();
        registry.RegisterVectorIndex(scope, denseIndex);
        registry.RegisterLexicalIndex(scope, lexicalIndex);
        registry.RegisterRecordProvider(
            scope,
            (targetScope, token) => targetScope.Kind == scope.Kind && targetScope.Id == scope.Id
                ? store.SnapshotAsync(scope.Kind.ToString(), scope.Id, token)
                : Task.FromResult<IReadOnlyList<MemoryRecord>>(Array.Empty<MemoryRecord>()));
        return new HybridRetriever(registry, embedder, new ReciprocalRankFuser(), new InMemoryMemoryTelemetry());
    }

    /// <summary>Vector index stub that always returns fixed prefixed ids at fixed scores.</summary>
    private sealed class FixedDenseIndex : IVectorIndex
    {
        private readonly string[] _ids;
        private readonly IVectorEmbedder _embedder;

        public FixedDenseIndex(IVectorEmbedder embedder, params string[] ids)
        {
            _embedder = embedder;
            _ids = ids;
        }

        public int Dim => 128;

        public long Count => _ids.Length;

        public Task BuildAsync(IReadOnlyList<VectorItem> items, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddAsync(VectorItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ScoredVectorId>> SearchAsync(IReadOnlyList<float> vector, int k, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ScoredVectorId> hits = _ids
                .Select((id, i) => new ScoredVectorId(id, 1.0f - (i * 0.01f)))
                .ToList();
            return Task.FromResult(hits);
        }
    }
}