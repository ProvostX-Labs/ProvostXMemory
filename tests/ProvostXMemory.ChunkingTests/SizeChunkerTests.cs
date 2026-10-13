using System;
using System.Linq;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Chunking;

using Xunit;

namespace ProvostXMemory.ChunkingTests;

/// <summary>SizeChunker: separator-aware splitting, lossless offsets, overlap sharing, clamps.</summary>
public sealed class SizeChunkerTests
{
    private static MemoryRecord Record(string content, string id = "r-1")
        => new(id, new MemoryScope(MemoryScopeKind.Session, "s1"), Key: $"{id}-key", content);

    [Fact]
    public void Short_content_is_one_chunk_with_exact_offsets()
    {
        var record = Record("hello world");
        var chunks = new SizeChunker().Split(record, new ChunkOptions(MaxChunkLength: 100));

        var chunk = Assert.Single(chunks);
        Assert.Equal("hello world", chunk.Content);
        Assert.Equal(0, chunk.StartOffset);
        Assert.Equal(11, chunk.EndOffset);
        Assert.Equal("Session|s1|r-1|0", chunk.ChunkId);
    }

    [Fact]
    public void Sentence_boundaries_preferred_and_whitespace_consumed()
    {
        var content = "Abc def. Ghij klmnop. Qrst uvw.";
        var record = Record(content);
        var chunks = new SizeChunker().Split(record, new ChunkOptions(MaxChunkLength: 12));

        // Terminator stays with its sentence; separator whitespace is consumed at cuts.
        Assert.Equal(new[] { "Abc def.", "Ghij klmnop.", "Qrst uvw." }, chunks.Select(c => c.Content).ToArray());
        Assert.All(chunks, c => Assert.Equal(c.Content, content[c.StartOffset..c.EndOffset]));
    }

    [Fact]
    public void Sentence_mode_slices_are_terminator_inclusive()
    {
        var record = Record("Abc def. Ghij klmnop. Qrst uvw.");
        var chunks = new SizeChunker().Split(record, new ChunkOptions(MaxChunkLength: 21));

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Abc def. Ghij klmnop.", chunks[0].Content);
        Assert.Equal("Qrst uvw.", chunks[1].Content);
        Assert.All(chunks, c => Assert.Equal(c.Content, "Abc def. Ghij klmnop. Qrst uvw."[c.StartOffset..c.EndOffset]));
    }

    [Fact]
    public void No_character_is_lost_when_reconstructing_without_separator_whitespace()
    {
        var record = Record("xx yy zz.");
        var chunks = new SizeChunker().Split(record, new ChunkOptions(MaxChunkLength: 2));

        var reconstructed = string.Concat(chunks.Select(c => c.Content));
        Assert.Equal("xxyyzz.", reconstructed);
    }

    [Fact]
    public void Hard_wrap_slices_exactly_max_chars()
    {
        var content = new string('a', 70);
        var record = Record(content);
        var chunks = new SizeChunker().Split(record, new ChunkOptions(MaxChunkLength: 30));

        Assert.Equal(3, chunks.Count);
        Assert.Equal(content[..30], chunks[0].Content);
        Assert.Equal(content[30..60], chunks[1].Content);
        Assert.Equal(content[60..], chunks[2].Content);
        Assert.All(chunks, c => Assert.Equal(c.Content, content[c.StartOffset..c.EndOffset]));
    }

    [Fact]
    public void Min_chunk_length_merges_short_trailing_chunk()
    {
        var content = "Aaa bbb ccc. Ddd.";
        var record = Record(content);
        var merged = new SizeChunker().Split(record, new ChunkOptions(MaxChunkLength: 12, MinChunkLength: 8));
        var unmerged = new SizeChunker().Split(record, new ChunkOptions(MaxChunkLength: 12));

        // With no min floor the 4-char tail stays its own chunk.
        Assert.Equal(2, unmerged.Count);
        Assert.Equal("Ddd.", unmerged[1].Content);
        Assert.Equal(13, unmerged[1].StartOffset);

        // The tail is below the floor, so it merges into the first chunk.
        var chunk = Assert.Single(merged);
        Assert.Equal(content, chunk.Content);
        Assert.Equal(0, chunk.StartOffset);
        Assert.Equal(content.Length, chunk.EndOffset);
    }

    [Fact]
    public void Whitespace_only_content_is_never_chunked()
    {
        Assert.Empty(new SizeChunker().Split(Record(""), new ChunkOptions()));
        Assert.Empty(new SizeChunker().Split(Record("   "), new ChunkOptions()));
        Assert.Empty(new SizeChunker().Split(Record("\n\n  \n"), new ChunkOptions()));
    }

    [Fact]
    public void Null_arguments_throw()
    {
        var chunker = new SizeChunker();
        Assert.Throws<ArgumentNullException>(() => chunker.Split(null!, new ChunkOptions()));
        Assert.Throws<ArgumentNullException>(() => chunker.Split(Record("x"), null!));
        Assert.Throws<ArgumentNullException>(() => new ChunkParentResolver(null!));
    }

    [Fact]
    public void Invalid_options_throw()
    {
        var chunker = new SizeChunker();
        Assert.Throws<ArgumentOutOfRangeException>(() => chunker.Split(Record("x"), new ChunkOptions(MaxChunkLength: -5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => chunker.Split(Record("x"), new ChunkOptions(MaxChunkLength: 10, Overlap: -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => chunker.Split(Record("x"), new ChunkOptions(MaxChunkLength: 10, Overlap: 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() => chunker.Split(Record("x"), new ChunkOptions(MaxChunkLength: 10, MinChunkLength: 11)));
    }

    [Fact]
    public void Separator_packing_joins_words_and_keeps_verbatim_offsets()
    {
        var record = Record("alpha beta gamma delta epsilon");
        var chunks = new SizeChunker()
            .Split(record, new ChunkOptions(MaxChunkLength: 11, Separator: "|"))
            .ToList();

        Assert.Equal(new[] { "alpha|beta", "gamma|delta", "epsilon" }, chunks.Select(c => c.Content).ToArray());
        Assert.Equal(0, chunks[0].StartOffset);
        Assert.Equal(10, chunks[0].EndOffset);
        Assert.Equal(11, chunks[1].StartOffset);
        Assert.Equal(22, chunks[1].EndOffset);
        Assert.Equal(23, chunks[2].StartOffset);
        Assert.Equal(30, chunks[2].EndOffset);
    }

    [Fact]
    public void Overlap_counts_words_and_shares_expected_text()
    {
        var record = Record("aa bb cc dd ee ff gg hh");
        var chunks = new SizeChunker()
            .Split(record, new ChunkOptions(MaxChunkLength: 11, Separator: " ", Overlap: 1))
            .ToList();

        // Greedy packing fills to the max ("aa bb cc dd" = 11 chars); each chunk then
        // opens with the previous chunk's last word (1-word rolling overlap).
        Assert.Equal(new[] { "aa bb cc dd", "dd ee ff gg", "gg hh" }, chunks.Select(c => c.Content).ToArray());
        for (var i = 1; i < chunks.Count; i++)
        {
            var previousTail = chunks[i - 1].Content.Split(' ')[^1];
            Assert.Equal(previousTail, chunks[i].Content.Split(' ')[0]);
        }
    }

    [Fact]
    public void Overlap_two_words_shares_a_two_word_run()
    {
        var record = Record("aa bb cc dd ee ff");
        var chunks = new SizeChunker()
            .Split(record, new ChunkOptions(MaxChunkLength: 9, Separator: " ", Overlap: 2))
            .ToList();

        // Rolling two-word overlap chain: each chunk shares its two head words with the
        // previous chunk's two tail words.
        Assert.Equal(new[] { "aa bb cc", "bb cc dd", "cc dd ee", "dd ee ff" }, chunks.Select(c => c.Content).ToArray());
        for (var i = 1; i < chunks.Count; i++)
        {
            var previousWords = chunks[i - 1].Content.Split(' ');
            var currentWords = chunks[i].Content.Split(' ');
            Assert.Equal(previousWords[^2..], currentWords[..2]);
        }
    }

    [Fact]
    public void Word_overlap_window_is_capped_to_fit_within_max()
    {
        var words = string.Join(" ", Enumerable.Repeat("word", 12));
        var record = Record(words);
        var chunks = new SizeChunker()
            .Split(record, new ChunkOptions(MaxChunkLength: 19, Separator: " ", Overlap: 3))
            .ToList();

        Assert.All(chunks, c => Assert.True(c.Content.Length <= 19, c.Content));
        for (var i = 1; i < chunks.Count; i++)
        {
            var previousTail = chunks[i - 1].Content.Split(' ')[^1];
            Assert.Equal(previousTail, chunks[i].Content.Split(' ')[0]);
        }
    }

    [Fact]
    public void Overlap_is_ignored_on_the_first_chunk()
    {
        var record = Record("solo entry");
        var chunk = Assert.Single(new SizeChunker()
            .Split(record, new ChunkOptions(MaxChunkLength: 100, Overlap: 4)));

        Assert.Equal("solo entry", chunk.Content);
        Assert.Equal(0, chunk.StartOffset);
        Assert.Equal(10, chunk.EndOffset);
    }

    [Fact]
    public void Character_overlap_windows_share_exactly_overlap_chars()
    {
        var content = new string('a', 100);
        var record = Record(content);
        var chunks = new SizeChunker()
            .Split(record, new ChunkOptions(MaxChunkLength: 30, Overlap: 10))
            .ToList();

        // Stride 20: [0,30), [20,50), [40,70), [60,90), [80,100).
        Assert.Equal(new[] { content[0..30], content[20..50], content[40..70], content[60..90], content[80..100] },
            chunks.Select(c => c.Content).ToArray());
        for (var i = 1; i < chunks.Count; i++)
        {
            Assert.Equal(10, chunks[i - 1].EndOffset - chunks[i].StartOffset);
        }
    }

    [Fact]
    public void Overlap_windows_never_lose_characters()
    {
        var content = new string('x', 55);
        var record = Record(content);
        var chunks = new SizeChunker()
            .Split(record, new ChunkOptions(MaxChunkLength: 20, Overlap: 5))
            .ToList();

        Assert.Equal(content.AsSpan(0, 15).ToString(), chunks[0].Content[..15]);
        Assert.Equal(content.Length, chunks[^1].EndOffset);
        Assert.All(chunks, c => Assert.Equal(c.Content, content[c.StartOffset..c.EndOffset]));
        for (var i = 1; i < chunks.Count; i++)
        {
            Assert.Equal(5, chunks[i - 1].EndOffset - chunks[i].StartOffset);
        }
    }

    [Fact]
    public void Chunks_never_exceed_max_and_offsets_stay_verbatim()
    {
        var rnd = new Random(42);
        for (var trial = 0; trial < 30; trial++)
        {
            var length = rnd.Next(1, 200);
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < length; i++)
            {
                sb.Append((char)('a' + (i % 23)));
            }

            foreach (var pos in new[] { 7, 19, 33, 61, 90 })
            {
                if (pos < sb.Length)
                {
                    sb[pos] = pos % 2 == 0 ? '.' : '\n';
                }
            }

            var record = Record(sb.ToString());
            var chunks = new SizeChunker().Split(record, new ChunkOptions(MaxChunkLength: rnd.Next(5, 60)));
            Assert.All(chunks, c => Assert.True(c.Content.Length <= 60, $"trial {trial}"));
            Assert.All(chunks, c => Assert.Equal(c.Content, sb.ToString()[c.StartOffset..c.EndOffset]));
        }
    }
}