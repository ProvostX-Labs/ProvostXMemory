using System;
using System.Collections.Generic;
using System.Text;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Chunking;

/// <summary>
/// Pure size + overlap splitting for unstructured content. Two modes:
/// geometry (no separator): with <see cref="ChunkOptions.Overlap"/> = 0 content is cut at
/// separators (paragraph break → newline → sentence terminator → hard slice of exactly
/// max) with separator whitespace consumed at cuts; with overlap > 0 fixed stride windows
/// (stride = max − overlap, window = max) guarantee consecutive chunks share exactly
/// <c>Overlap</c> characters and no character is lost. Separator mode: words are packed
/// and joined by the separator; <c>Overlap</c> counts shared whole words repeated from
/// the previous chunk's tail (rolling window), so shared text matches word for word.
/// </summary>
public sealed class SizeChunker : IChunker
{
    private readonly ChunkIdCodec _codec;

    public SizeChunker(ChunkIdCodec? codec = null)
    {
        _codec = codec ?? new ChunkIdCodec();
    }

    /// <inheritdoc />
    public IReadOnlyList<Chunk> Split(MemoryRecord record, ChunkOptions options)
    {
        ChunkAssembler.Instance.Validate(record, options);

        return string.IsNullOrEmpty(options.Separator)
            ? SplitByGeometry(record, options)
            : SplitBySeparatorWords(record, options);
    }

    private IReadOnlyList<Chunk> SplitByGeometry(MemoryRecord record, ChunkOptions options)
    {
        var segments = options.Overlap > 0
            ? StrideWindows(record.Content, options.MaxChunkLength, options.Overlap)
            : Segmenter.Instance.Segments(record.Content, options.MaxChunkLength);

        return ChunkAssembler.Instance.Assemble(
            record.Id,
            record.Content,
            record.Tags,
            segments,
            options,
            index => _codec.Compose(record.Scope, record.Id, index));
    }

    // Overlap windows: chunk i spans [i·stride, i·stride + max); consecutive chunks
    // share exactly `overlap` characters; no character is skipped.
    private static IReadOnlyList<Segment> StrideWindows(string content, int max, int overlap)
    {
        var windows = new List<Segment>();
        if (string.IsNullOrEmpty(content))
        {
            return windows;
        }

        var stride = max - overlap;
        for (var start = 0; start < content.Length; start += stride)
        {
            var end = Math.Min(start + max, content.Length);
            windows.Add(new Segment(start, end));
            if (end >= content.Length)
            {
                break;
            }
        }

        return windows;
    }

    private IReadOnlyList<Chunk> SplitBySeparatorWords(MemoryRecord record, ChunkOptions options)
    {
        var separator = options.Separator!;
        var words = Segmenter.Instance.Words(record.Content);
        if (words.Count == 0)
        {
            return Array.Empty<Chunk>();
        }

        var chunks = new List<Chunk>();
        var previousFirst = 0;
        var previousLast = -1; // last word index covered by the previous chunk
        var position = 0;      // first word NOT yet covered by any chunk
        while (position < words.Count)
        {
            var sharedCount = 0;
            if (chunks.Count > 0 && options.Overlap > 0 && previousLast >= previousFirst)
            {
                sharedCount = Math.Min(options.Overlap, previousLast - previousFirst + 1);
            }

            var first = sharedCount > 0 ? previousLast - sharedCount + 1 : position;
            var builder = new StringBuilder(JoinWords(words, first, sharedCount > 0 ? previousLast : first - 1, separator));

            // Pack new words while the chunk stays within the max.
            while (position < words.Count)
            {
                var pendingSeparator = builder.Length == 0 ? 0 : separator.Length;
                if (builder.Length > 0
                    && builder.Length + pendingSeparator + words[position].Text.Length > options.MaxChunkLength)
                {
                    break;
                }

                if (builder.Length > 0)
                {
                    builder.Append(separator);
                }

                builder.Append(words[position].Text);
                position++;
            }

            var last = Math.Max(position - 1, first);
            chunks.Add(new Chunk(
                _codec.Compose(record.Scope, record.Id, chunks.Count),
                record.Id,
                builder.ToString(),
                Index: chunks.Count,
                Tags: record.Tags,
                StartOffset: words[first].Start,
                EndOffset: words[last].End));

            previousFirst = first;
            previousLast = last;
        }

        return chunks;
    }

    // Joins words[start..end] (inclusive) with the separator; empty when range is empty.
    private static string JoinWords(IReadOnlyList<WordSpan> words, int start, int end, string separator)
    {
        if (end < start)
        {
            return string.Empty;
        }

        var joined = words[start].Text;
        for (var i = start + 1; i <= end; i++)
        {
            joined += separator + words[i].Text;
        }

        return joined;
    }
}