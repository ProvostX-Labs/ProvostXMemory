using System;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Retrieval;
using ProvostXMemory.VectorIndex;

using Xunit;

namespace ProvostXMemory.RetrievalTests;

/// <summary>MultiRepresentationRetriever: per-modality representation hits merge to parent records.</summary>
public sealed class MultiRepresentationRetrieverTests
{
    private static (InMemoryScopeIndexRegistry Registry, MemoryScope Scope, HashEmbedder Embedder) Arrange()
    {
        var registry = new InMemoryScopeIndexRegistry();
        return (registry, new MemoryScope(MemoryScopeKind.Session, "s1"), new HashEmbedder());
    }

    private static MemoryRecord RecordWithReps(string id, string content, string[] representationTexts)
        => new(
            Id: id,
            Scope: new MemoryScope(MemoryScopeKind.Session, "s1"),
            Key: id,
            Content: content)
        {
            Reps = representationTexts
                .Select(text => new MemoryRepresentation(MemoryRepresentationModality.Text, text))
                .ToList(),
        };

    [Fact]
    public async Task Query_matching_representation_returns_parent_record()
    {
        var (registry, scope, embedder) = Arrange();
        var index = new FlatJsonVectorIndex(
            new VectorNamespaceFile(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pvmx-mr-{Guid.NewGuid():N}", "index.json"),
                embedder.ModelHash,
                embedder.Dim),
            new VectorIndexGuard(embedder.ModelHash, embedder.Dim));

        var parent = RecordWithReps(
            "photo-rec",
            "meeting notes about the quarterly budget",
            new[] { "quarterly budget chart summary", "unrelated caption text" });
        registry.RegisterVectorIndex(scope, index);
        registry.RegisterRecordProvider(scope, (_, _) => Task.FromResult<IReadOnlyList<MemoryRecord>>(new[] { parent }));

        // Index only the representations (the retriever consumes repr| ids).
        var items = new System.Collections.Generic.List<VectorItem>
        {
            new(MultiRepresentationRetriever.RepresentationId(parent.Id, 0), await embedder.EmbedAsync(parent.Reps[0].Content)),
            new(MultiRepresentationRetriever.RepresentationId(parent.Id, 1), await embedder.EmbedAsync(parent.Reps[1].Content)),
        };
        await index.BuildAsync(items);

        var retriever = new MultiRepresentationRetriever(registry, embedder);
        var result = await retriever.RetrieveAsync(new RetrievalQuery("quarterly budget chart", scope, 3), MemoryRepresentationModality.Text);

        var hit = Assert.Single(result.Records);
        Assert.Equal("photo-rec", hit.Record.Id);
        Assert.Contains("repr.Text", hit.Provenance.StageScores.Keys);
    }

    [Fact]
    public async Task Best_representation_score_wins_and_ties_break_by_record_id()
    {
        var (registry, scope, embedder) = Arrange();
        var index = new FlatJsonVectorIndex(
            new VectorNamespaceFile(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pvmx-mr2-{Guid.NewGuid():N}", "index.json"),
                embedder.ModelHash,
                embedder.Dim),
            new VectorIndexGuard(embedder.ModelHash, embedder.Dim));
        registry.RegisterVectorIndex(scope, index);

        var first = RecordWithReps("b-first", "notes", new[] { "shared term" });
        var second = RecordWithReps("a-second", "notes", new[] { "shared term", "shared term precise" });
        registry.RegisterRecordProvider(
            scope,
            (_, _) => Task.FromResult<IReadOnlyList<MemoryRecord>>(new[] { first, second }));

        var items = new System.Collections.Generic.List<VectorItem>();
        foreach (var record in new[] { first, second })
        {
            for (var i = 0; i < record.Reps.Count; i++)
            {
                items.Add(new VectorItem(
                    MultiRepresentationRetriever.RepresentationId(record.Id, i),
                    await embedder.EmbedAsync(record.Reps[i].Content)));
            }
        }

        await index.BuildAsync(items);
        await index.FlushAsync();

        var retriever = new MultiRepresentationRetriever(registry, embedder);
        var result = await retriever.RetrieveAsync(new RetrievalQuery("shared term", scope, 5), MemoryRepresentationModality.Text);

        Assert.Equal(2, result.Records.Count);
        // Order is score desc, then record id — the deterministic contract.
        Assert.True(result.Records[0].Score >= result.Records[1].Score);
        if (Math.Abs(result.Records[0].Score - result.Records[1].Score) < 1e-9)
        {
            Assert.Equal("a-second", result.Records[0].Record.Id);
        }
        else
        {
            // The record whose best representation scores higher must lead.
            Assert.Equal("a-second", result.Records[0].Record.Id);
        }
    }

    [Fact]
    public void Representation_id_roundtrip()
    {
        var id = MultiRepresentationRetriever.RepresentationId("rec-7", 2);
        Assert.Equal("repr|rec-7|2", id);
        Assert.Equal("rec-7", MultiRepresentationRetriever.ParseParentId(id));
        Assert.Null(MultiRepresentationRetriever.ParseParentId("record:rec-7"));
        Assert.Null(MultiRepresentationRetriever.ParseParentId("no-prefix-here"));
    }
    /// <summary>
    /// Regression pin (2026-10-14 final audit): a recordId containing the id separator
    /// produces representation ids ParseParentId can never decode (silent hit loss) —
    /// RepresentationId must reject loudly at compose time (same discipline as ChunkIdCodec).
    /// </summary>
    [Fact]
    public void RepresentationId_RejectsPipeInRecordId()
    {
        Assert.Throws<ArgumentException>(() => MultiRepresentationRetriever.RepresentationId("re|c", 0));
        Assert.Equal("repr|rec|0", MultiRepresentationRetriever.RepresentationId("rec", 0));
    }

}
