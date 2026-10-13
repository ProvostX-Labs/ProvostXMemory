using System;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Graph;

using Xunit;

namespace ProvostXMemory.GraphTests;

/// <summary>Heuristic extractor: capitalized phrases, tags/keywords, co-occurrence relations, idempotence.</summary>
public sealed class HeuristicEntityExtractorTests
{
    [Fact]
    public async Task Extracts_capitalized_multi_word_phrases_and_sentence_leads()
    {
        var extractor = new HeuristicEntityExtractor();
        var result = await extractor.ExtractAsync(new[]
        {
            Record("rec-1", "Frank Lloyd Wright designed the studio. Grace Hopper visited it."),
        });

        var ids = result.Entities.Select(e => e.EntityId).ToList();
        Assert.Contains("frank-lloyd-wright", ids);
        Assert.Contains("grace-hopper", ids);
        // lowercase words and second-sentence single words are not entities
        Assert.DoesNotContain("designed", ids);
        Assert.DoesNotContain("the", ids);
        Assert.Equal("Frank Lloyd Wright", result.Entities.Single(e => e.EntityId == "frank-lloyd-wright").Name);
    }

    [Fact]
    public async Task Single_capitalized_mention_accumulates_record_references()
    {
        var extractor = new HeuristicEntityExtractor();
        var result = await extractor.ExtractAsync(new[]
        {
            Record("rec-1", "Ada Lovelace wrote notes."),
            Record("rec-2", "Ada Lovelace again here."),
            Record("rec-3", "no entities in this sentence"),
        });

        var ada = result.Entities.Single(e => e.EntityId == "ada-lovelace");
        Assert.Equal(new[] { "rec-1", "rec-2" }, ada.RecordIds);
    }

    [Fact]
    public async Task Tags_and_keywords_become_entities()
    {
        var extractor = new HeuristicEntityExtractor(new[] { "kubernetes" });
        var result = await extractor.ExtractAsync(new[]
        {
            new MemoryRecord(
                Id: "rec-9",
                Scope: new MemoryScope(MemoryScopeKind.Session, "s1"),
                Key: "k",
                Content: "plain lowercase content about deploy tooling on kubernetes clusters",
                Tags: new[] { "Docker", "deploy" }),
        });

        var ids = result.Entities.Select(e => e.EntityId).ToList();
        Assert.Contains("docker", ids);
        Assert.Contains("kubernetes", ids);
        Assert.Contains("deploy", ids); // every tag becomes an entity by design
    }

    [Fact]
    public async Task Co_occurrence_within_record_creates_relation_with_support_count()
    {
        var extractor = new HeuristicEntityExtractor();
        var result = await extractor.ExtractAsync(new[]
        {
            Record("rec-1", "Alice Smith met Bob Jones."),
            Record("rec-2", "Alice Smith called Bob Jones again."),
            Record("rec-3", "Bob Jones worked alone."),
        });

        var relation = result.Relations.Single(r => r.RelationId == "alice-smith+bob-jones");
        Assert.Equal("alice-smith", relation.FromEntityId);
        Assert.Equal("bob-jones", relation.ToEntityId);
        Assert.Equal(new[] { "rec-1", "rec-2" }, relation.RecordIds);
    }

    [Fact]
    public async Task Deterministic_output_for_same_input()
    {
        var extractor = new HeuristicEntityExtractor();
        var records = new[]
        {
            Record("rec-1", "Zeta Ray spoke with Alpha One."),
        };

        var first = await extractor.ExtractAsync(records);
        var second = await extractor.ExtractAsync(records);

        Assert.Equal(
            first.Entities.Select(e => e.EntityId),
            second.Entities.Select(e => e.EntityId));
        Assert.Equal(
            first.Relations.Select(r => r.RelationId),
            second.Relations.Select(r => r.RelationId));
    }

    [Fact]
    public async Task Extractor_can_be_replaced_by_host_injected_implementation()
    {
        // Hosts inject stronger extractors via IEntityExtractor; the heuristic default is replaceable.
        IEntityExtractor extractor = new RecordingExtractor();
        await extractor.ExtractAsync(new[] { Record("rec-1", "anything at all") });
        Assert.True(RecordingExtractor.WasCalled);
    }

    private static MemoryRecord Record(string id, string content)
    {
        return new MemoryRecord(
            Id: id,
            Scope: new MemoryScope(MemoryScopeKind.Session, "s1"),
            Key: id,
            Content: content);
    }

    private sealed class RecordingExtractor : IEntityExtractor
    {
        public static bool WasCalled { get; private set; }

        public Task<ExtractionResult> ExtractAsync(IReadOnlyList<MemoryRecord> records, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            var result = new ExtractionResult(Array.Empty<GraphEntity>(), Array.Empty<GraphRelation>());
            return Task.FromResult(result);
        }
    }
}