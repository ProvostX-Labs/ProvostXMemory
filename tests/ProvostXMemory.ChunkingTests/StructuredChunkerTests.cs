using System;
using System.Linq;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Chunking;

using Xunit;

namespace ProvostXMemory.ChunkingTests;

/// <summary>StructuredChunker: headings, paragraphs, sentence fallback, min/max clamps, offsets, ids.</summary>
public sealed class StructuredChunkerTests
{
    private static MemoryRecord Record(string content, string id = "r-1")
        => new(id, new MemoryScope(MemoryScopeKind.Tenant, "acme"), Key: $"{id}-key", content);

    [Fact]
    public void Headings_become_their_own_chunks_keeping_the_hash_prefix()
    {
        var record = Record("# Alpha\nBody of alpha.\n\n## Beta\nBody of beta.");
        var chunks = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 200));

        Assert.Equal(4, chunks.Count);
        Assert.Equal("# Alpha", chunks[0].Content);
        Assert.Equal("Body of alpha.", chunks[1].Content);
        Assert.Equal("## Beta", chunks[2].Content);
        Assert.Equal("Body of beta.", chunks[3].Content);
        Assert.StartsWith("#", chunks[0].Content);
        Assert.StartsWith("##", chunks[2].Content);
    }

    [Fact]
    public void Headings_before_empty_line_are_their_own_chunk_without_blank_line()
    {
        var record = Record("# Title\n\nBody.");
        var chunks = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 100));

        Assert.Equal(2, chunks.Count);
        Assert.Equal("# Title", chunks[0].Content);
        Assert.Equal("Body.", chunks[1].Content);
    }

    [Fact]
    public void Chunks_carry_verbatim_offsets_back_into_the_record()
    {
        var content = "Intro here.\n\n# Section\nSection words.\n\nTail note.";
        var record = Record(content);
        var chunks = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 500));

        Assert.All(chunks, c => Assert.Equal(c.Content, content[c.StartOffset..c.EndOffset]));
        Assert.All(chunks, c => Assert.True(c.EndOffset > c.StartOffset));
    }

    [Fact]
    public void Offsets_cover_all_content_in_order()
    {
        var content = "First block.\n\nSecond block.\n\n# Head\nHead body.";
        var record = Record(content);
        var chunks = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 400));

        Assert.Equal(content.Length, chunks[^1].EndOffset);
        for (var i = 1; i < chunks.Count; i++)
        {
            Assert.True(chunks[i].StartOffset >= chunks[i - 1].EndOffset);
        }
    }

    [Fact]
    public void Paragraphs_at_or_under_max_stay_whole()
    {
        var a = new string('a', 40);
        var b = new string('b', 39);
        var record = Record($"{a}\n\n{b}");
        var chunks = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 40));

        Assert.Equal(2, chunks.Count);
        Assert.Equal(a, chunks[0].Content);
        Assert.Equal(b, chunks[1].Content);
    }

    [Fact]
    public void Oversized_paragraph_falls_back_to_sentences_within_max()
    {
        var s1 = "First sentence stands alone here.";
        var s2 = "Second sentence follows immediately.";
        var s3 = "Third sentence ends the paragraph.";
        var paragraph = $"{s1} {s2} {s3}";
        var record = Record(paragraph);
        var chunks = new StructuredChunker()
            .Split(record, new ChunkOptions(MaxChunkLength: s1.Length + 1 + s2.Length + 1))
            .ToList();

        Assert.Equal(2, chunks.Count);
        Assert.Equal($"{s1} {s2}", chunks[0].Content);
        Assert.Equal(s3, chunks[1].Content);
    }

    [Fact]
    public void Every_no_overlap_chunk_respects_max()
    {
        var rnd = new Random(20261013);
        for (var trial = 0; trial < 20; trial++)
        {
            var max = rnd.Next(8, 80);
            var content = $"{new string('x', max + 5)}\n\n{new string('y', max + 3)} zz {new string('w', max + 1)}.";
            var record = Record(content);
            var chunks = new StructuredChunker()
                .Split(record, new ChunkOptions(MaxChunkLength: max));
            Assert.All(chunks, c => Assert.True(c.Content.Length <= max, $"trial {trial}: {c.Content.Length} > {max}"));
        }
    }

    [Fact]
    public void Chunk_ids_are_scope_bound_and_ordered()
    {
        var record = Record("# H\nBody.", id: "rec-42");
        var chunks = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 500));

        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, c => Assert.Equal("rec-42", c.RecordId));
        Assert.Equal("Tenant|acme|rec-42|0", chunks[0].ChunkId);
        Assert.Equal("Tenant|acme|rec-42|1", chunks[1].ChunkId);
        Assert.Equal(0, chunks[0].Index);
        Assert.Equal(1, chunks[1].Index);
    }

    [Fact]
    public void Non_structural_mode_matches_the_size_chunker()
    {
        var line1 = new string('a', 30);
        var line2 = new string('b', 30);
        var record = Record($"{line1}\n{line2}");
        var options = new ChunkOptions(MaxChunkLength: 35, RespectStructure: false);
        var structured = new StructuredChunker().Split(record, options);
        var sized = new SizeChunker().Split(record, options);

        Assert.Equal(2, structured.Count);
        Assert.Equal(line1, structured[0].Content);
        Assert.Equal(line2, structured[1].Content);
        Assert.Equal(sized.Select(c => c.Content), structured.Select(c => c.Content));
        Assert.Equal(sized.Select(c => c.ChunkId), structured.Select(c => c.ChunkId));
    }

    [Fact]
    public void Overlap_is_ignored_structure_paragraphs_stay_intact()
    {
        var record = Record("# Alpha\nBody of alpha.\n\n## Beta\nBody of beta.");
        var plain = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 200));
        var withOverlap = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 200, Overlap: 10));

        // Structural chunking preserves paragraphs; overlap is a size-mode concept.
        Assert.Equal(plain.Select(c => c.Content), withOverlap.Select(c => c.Content));
    }

    [Fact]
    public void Trailing_short_paragraph_merges_into_previous_chunk()
    {
        var big = new string('a', 30);
        var tail = "tail!";
        var record = Record($"{big}\n\n{tail}");
        var chunks = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 30, MinChunkLength: 10));

        Assert.Single(chunks);
        Assert.Equal($"{big}\n\n{tail}", chunks[0].Content);
        Assert.Equal($"{big}\n\n{tail}".Length, chunks[0].EndOffset);
    }

    [Fact]
    public void Min_floor_disabled_keeps_short_paragraphs_separate()
    {
        var big = new string('a', 30);
        var record = Record($"{big}\n\ntail!");
        var chunks = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 30));

        Assert.Equal(2, chunks.Count);
        Assert.Equal("tail!", chunks[1].Content);
    }

    [Fact]
    public void Empty_content_yields_no_chunks()
    {
        var chunker = new StructuredChunker();
        Assert.Empty(chunker.Split(Record(""), new ChunkOptions()));
        Assert.Empty(chunker.Split(Record("   "), new ChunkOptions()));
    }

    [Fact]
    public void Null_record_or_options_throw()
    {
        var chunker = new StructuredChunker();
        Assert.Throws<ArgumentNullException>(() => chunker.Split(null!, new ChunkOptions()));
        Assert.Throws<ArgumentNullException>(() => chunker.Split(Record("x"), null!));
    }

    [Fact]
    public void Invalid_options_throw()
    {
        var chunker = new StructuredChunker();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => chunker.Split(Record("x"), new ChunkOptions(MaxChunkLength: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => chunker.Split(Record("x"), new ChunkOptions(MaxChunkLength: 10, Overlap: 10)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => chunker.Split(Record("x"), new ChunkOptions(MaxChunkLength: 10, MinChunkLength: 11)));
    }

    [Fact]
    public void Content_without_structural_markers_hard_wraps_at_max()
    {
        var longText = new string('z', 50);
        var record = Record(longText);
        var chunks = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 20));

        Assert.Equal(3, chunks.Count);
        Assert.Equal(longText[..20], chunks[0].Content);
        Assert.Equal(longText[20..40], chunks[1].Content);
        Assert.Equal(longText[40..], chunks[2].Content);
    }

    [Fact]
    public void Sentence_fallback_packs_within_max_bound()
    {
        var sentence = "One two three four five.";
        var paragraph = string.Join(" ", Enumerable.Repeat(sentence, 8));
        var record = Record(paragraph);
        var chunks = new StructuredChunker()
            .Split(record, new ChunkOptions(MaxChunkLength: 55))
            .ToList();

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Content.Length <= 55));
        Assert.All(chunks, c => Assert.StartsWith("One", c.Content));
    }

    [Fact]
    public void Chunks_inherit_record_tags()
    {
        var record = new MemoryRecord(
            "r-tags",
            new MemoryScope(MemoryScopeKind.Session, "s1"),
            Key: "k",
            Content: "# H\nBody.",
            Tags: new[] { "alpha", "beta" });
        var chunks = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 100));

        Assert.All(chunks, c => Assert.Equal(new[] { "alpha", "beta" }, c.Tags));
        Assert.All(chunks, c => Assert.Equal("r-tags", c.RecordId));
    }

    [Fact]
    public void High_min_floor_merges_all_pieces_below_it()
    {
        var record = Record("tiny paragraph below floor\n\nAnother paragraph, sufficiently long here.");
        var merged = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 50, MinChunkLength: 50));

        // Every chunk is below the floor, so all pieces cascade into one chunk.
        var chunk = Assert.Single(merged);
        Assert.Equal(record.Content, chunk.Content);
        Assert.Equal(record.Content.Length, chunk.EndOffset);
    }

    [Fact]
    public void Indented_heading_still_counts_as_heading()
    {
        var record = Record("  ## Indented\nBody line.");
        var chunks = new StructuredChunker().Split(record, new ChunkOptions(MaxChunkLength: 100));

        Assert.Equal(2, chunks.Count);
        Assert.Equal("  ## Indented", chunks[0].Content);
    }
}