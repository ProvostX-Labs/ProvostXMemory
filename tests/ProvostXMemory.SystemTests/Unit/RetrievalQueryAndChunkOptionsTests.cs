using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Chunking;
using ProvostXMemory.Fusion;
using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;

using Xunit;

namespace ProvostXMemory.SystemTests.Unit;

/// <summary>
/// RetrievalQuery/filter behavioral semantics: the query model is an immutable record —
/// defaults (TopK 10, no filters, no plan), filter payloads preserved verbatim, and
/// top-k meaning (the cut the pipeline may never exceed). Type-level behavior of the
/// query model itself; filters are applied by hosts above the library surface.
/// </summary>
public sealed class RetrievalQueryFilterTests
{
    private static MemoryScope Scope(string id = "s1") => new(MemoryScopeKind.Session, id);

    [Fact]
    public void Defaults_are_topk_10_no_filters_no_plan()
    {
        var query = new RetrievalQuery("find the kubernetes rollout", Scope());

        Assert.Equal(10, query.TopK);
        Assert.Null(query.Filters);
        Assert.Null(query.Plan);
    }

    [Fact]
    public void Simplest_construction_matches_positional_defaults()
    {
        var explicitAll = new RetrievalQuery("text", Scope(), TopK: 10, Filters: null, Plan: null);
        var positional = new RetrievalQuery("text", Scope());
        var withOnlyScope = new RetrievalQuery("text", Scope(), 10);
        var minimal = new RetrievalQuery("text", Scope(), 10, null);

        Assert.Equal(explicitAll, positional);
        Assert.Equal(explicitAll, withOnlyScope);
        Assert.Equal(explicitAll, minimal);
    }

    [Fact]
    public void Filters_preserve_tags_category_and_date_window()
    {
        var from = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
        var query = new RetrievalQuery(
            "invoice",
            Scope(),
            TopK: 5,
            Filters: new RetrievalFilters(
                Tags: new[] { "billing", "active" },
                Category: "finance",
                From: from,
                To: to));

        Assert.NotNull(query.Filters);
        Assert.Equal(new[] { "billing", "active" }, query.Filters.Tags);
        Assert.Equal("finance", query.Filters.Category);
        Assert.Equal(from, query.Filters.From);
        Assert.Equal(to, query.Filters.To);
    }

    [Fact]
    public void Filter_tags_default_to_empty_not_null_when_unspecified()
    {
        // The record normalizes a NULL tags list to an empty (never-null) collection —
        // callers may destructure without null checks.
        Assert.Empty(new RetrievalFilters().Tags);
        Assert.Empty(new RetrievalFilters(Category: "ops").Tags);
        Assert.Equal("ops", new RetrievalFilters(Category: "ops").Category);
        Assert.Null(new RetrievalFilters(Category: "ops").From);
        Assert.Null(new RetrievalFilters(Category: "ops").To);
    }

    [Fact]
    public void Explicit_null_tags_also_normalize_to_empty()
    {
        Assert.Empty(new RetrievalFilters(Tags: null).Tags);
    }

    [Fact]
    public void With_filters_creates_a_pristine_copy()
    {
        var original = new RetrievalQuery("original", Scope(), TopK: 3);
        var copied = original with { Filters = new RetrievalFilters(Tags: new[] { "t" }) };

        Assert.Null(original.Filters);
        Assert.Single(copied.Filters!.Tags);
        Assert.Equal("original", copied.Text); // remainder unchanged
        Assert.Equal(3, copied.TopK);
        Assert.Equal(original.Scope, copied.Scope);
    }

    [Fact]
    public void TopK_is_a_hard_record_member_not_a_derived_value()
    {
        var one = new RetrievalQuery("q", Scope(), TopK: 1);
        var large = new RetrievalQuery("q", Scope(), TopK: 500);

        Assert.Equal(1, one.TopK);
        Assert.Equal(500, large.TopK);
    }

    [Fact]
    public void Date_window_offsets_compare_in_value_space()
    {
        var earlier = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var later = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.True(earlier < later);
        Assert.False(earlier > later);
        Assert.Equal(earlier, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Query_equality_covers_all_members()
    {
        var scope = Scope();
        var filters = new RetrievalFilters(Category: "ops");
        var left = new RetrievalQuery("same", scope, TopK: 7, Filters: filters);
        var right = new RetrievalQuery("same", scope, TopK: 7, Filters: filters);

        Assert.Equal(left, right);
        Assert.NotEqual(left, left with { TopK = 8 });
        Assert.NotEqual(left, left with { Text = "other" });
        Assert.NotEqual(left, left with { Filters = null });
    }
}

/// <summary>
/// ChunkOptions edge-value behavior through the real StructuredChunker: boundary
/// validation, single-word/empty content, per-character splitting, trailing
/// MinChunkLength merge, structural markers, and offset invariants. Complements the
/// SizeChunker suite with the production-default chunker.
/// </summary>
public sealed class ChunkOptionsEdgeTests
{
    private static MemoryRecord Record(string content) =>
        new("r1", new MemoryScope(MemoryScopeKind.Session, "s"), "r1", content);

    [Fact]
    public void Zero_max_chunk_length_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StructuredChunker().Split(Record("alpha beta"), new ChunkOptions(MaxChunkLength: 0)));
    }

    [Fact]
    public void Overlap_equal_to_max_throws_but_overlap_just_below_is_valid()
    {
        var chunker = new StructuredChunker();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => chunker.Split(Record("alpha beta gamma"), new ChunkOptions(MaxChunkLength: 5, Separator: " ", Overlap: 5)));

        var valid = chunker.Split(Record("alpha beta gamma"), new ChunkOptions(MaxChunkLength: 5, Separator: " ", Overlap: 4));
        Assert.NotEmpty(valid);
    }

    [Fact]
    public void Min_length_above_max_throws_but_equal_max_is_valid()
    {
        var chunker = new StructuredChunker();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => chunker.Split(Record("alpha beta gamma"), new ChunkOptions(MaxChunkLength: 10, MinChunkLength: 11)));

        var valid = chunker.Split(Record("alpha beta gamma"), new ChunkOptions(MaxChunkLength: 10, MinChunkLength: 10));
        Assert.NotEmpty(valid);
    }

    [Fact]
    public void Empty_content_yields_zero_chunks()
    {
        var chunks = new StructuredChunker().Split(Record(""), new ChunkOptions());

        Assert.Empty(chunks);
    }

    [Fact]
    public void Single_word_fits_one_chunk_even_when_shorter_than_max()
    {
        var chunks = new StructuredChunker().Split(Record("solitary"), new ChunkOptions(MaxChunkLength: 100));

        Assert.Single(chunks);
        Assert.Equal("solitary", chunks[0].Content);
        Assert.Equal(0, chunks[0].Index);
    }

    [Fact]
    public void Max_length_one_splits_into_single_characters()
    {
        var chunks = new StructuredChunker().Split(
            Record("abcdefghijklmnopqrstuvwxyz"),
            new ChunkOptions(MaxChunkLength: 1));

        Assert.Equal(26, chunks.Count);
        Assert.All(chunks, chunk => Assert.Equal(1, chunk.Content.Length));
        Assert.Equal(Enumerable.Range(0, 26), chunks.Select(chunk => chunk.Index));
    }

    [Fact]
    public void Trailing_short_chunk_merges_into_predecessor_beyond_max()
    {
        var chunks = new StructuredChunker().Split(
            Record("aa bb cc dd ee ff gg"),
            new ChunkOptions(MaxChunkLength: 6, Separator: " ", MinChunkLength: 4));

        // The trailing remainder is short of Min=4 → merged into the predecessor,
        // which may then exceed MaxChunkLength (documented behavior).
        Assert.Equal(3, chunks.Count);
        Assert.Equal("ee ff gg", chunks[2].Content.TrimEnd(' '));
        Assert.True(chunks[2].Content.TrimEnd(' ').Length > 6);
    }

    [Fact]
    public void Respect_structure_splits_exactly_at_line_boundaries()
    {
        var chunks = new StructuredChunker().Split(
            Record("alpha beta\ngamma delta\nepsilon zeta"),
            new ChunkOptions(MaxChunkLength: 12, Separator: " "));

        Assert.Equal(3, chunks.Count);
        Assert.Equal("alpha beta", chunks[0].Content.TrimEnd(' '));
        Assert.Equal("gamma delta", chunks[1].Content.TrimEnd(' '));
        Assert.Equal("epsilon zeta", chunks[2].Content.TrimEnd(' '));
    }

    [Fact]
    public void Respect_structure_false_packs_across_line_boundaries()
    {
        var chunks = new StructuredChunker().Split(
            Record("alpha beta\ngamma delta\nepsilon zeta"),
            new ChunkOptions(MaxChunkLength: 11, Separator: " ", RespectStructure: false));

        Assert.True(chunks.Count >= 2);
        var packed = string.Join(' ', chunks.Select(chunk => chunk.Content.TrimEnd(' ')));
        Assert.Equal("alpha beta gamma delta epsilon zeta", packed);
        // The split lands mid-line: boundaries differ from the structured variant.
        Assert.NotEqual(
            new[] { "alpha beta", "gamma delta", "epsilon zeta" },
            chunks.Select(chunk => chunk.Content.TrimEnd(' ')).ToArray());
    }

    [Fact]
    public void Chunk_ids_and_offsets_are_consistent_with_the_parent_content()
    {
        var record = Record("alpha beta gamma delta");
        var chunks = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 7, Separator: " "));

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk =>
        {
            Assert.Equal("r1", chunk.RecordId);
            Assert.StartsWith("Session|s|r1|", chunk.ChunkId, StringComparison.Ordinal);
            Assert.True(chunk.StartOffset >= 0);
            Assert.True(chunk.EndOffset > chunk.StartOffset);
            Assert.Equal(record.Content.Substring(chunk.StartOffset, chunk.EndOffset - chunk.StartOffset), chunk.Content);
        });
        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(chunk => chunk.Index));
    }

    [Fact]
    public void Default_options_are_512_no_overlap_structure_respecting()
    {
        var options = new ChunkOptions();

        Assert.Equal(512, options.MaxChunkLength);
        Assert.Equal(0, options.Overlap);
        Assert.True(options.RespectStructure);
        Assert.Equal(0, options.MinChunkLength);
        Assert.Null(options.Separator);
    }

    [Fact]
    public void Long_content_spans_multiple_default_chunks_with_512_cap()
    {
        var longContent = string.Join(' ', Enumerable.Repeat("lorem ipsum dolor sit amet consectetur", 20));
        var chunks = new StructuredChunker().Split(Record(longContent), new ChunkOptions());

        Assert.True(chunks.Count >= 2);
        Assert.All(chunks, chunk => Assert.True(chunk.Content.Length <= 512, $"chunk too long: {chunk.Content.Length}"));
    }

    [Fact]
    public void Every_chunk_inherits_the_record_tags()
    {
        var record = new MemoryRecord(
            Id: "tagged",
            Scope: new MemoryScope(MemoryScopeKind.Session, "s"),
            Key: "tagged",
            Content: "alpha beta gamma")
        {
            Tags = new[] { "kept", "inherited" },
        };

        var chunks = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 7, Separator: " "));

        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk => Assert.Equal(record.Tags, chunk.Tags));
    }
}