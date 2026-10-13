using System;
using System.Collections.Generic;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Chunking;

/// <summary>
/// Splits record content by markdown structure: headings become their own chunks, each
/// paragraph is a chunk (or sentence-packed pieces when oversized; a single over-max
/// sentence hard-slices). Non-structural mode (<see cref="ChunkOptions.RespectStructure"/>
/// = false) delegates to <see cref="SizeChunker"/>. Overlap is only applied by
/// <see cref="SizeChunker"/>; structural chunking keeps paragraphs intact. All content is
/// preserved verbatim (modulo whitespace between structural cuts) with exact offsets.
/// </summary>
public sealed class StructuredChunker : IChunker
{
    private readonly ChunkIdCodec _codec;

    public StructuredChunker(ChunkIdCodec? codec = null)
    {
        _codec = codec ?? new ChunkIdCodec();
    }

    /// <inheritdoc />
    public IReadOnlyList<Chunk> Split(MemoryRecord record, ChunkOptions options)
    {
        ChunkAssembler.Instance.Validate(record, options);

        if (!options.RespectStructure)
        {
            return new SizeChunker(_codec).Split(record, options);
        }

        return ChunkAssembler.Instance.Assemble(
            record.Id,
            record.Content,
            record.Tags,
            StructuralSegments(record.Content, options.MaxChunkLength),
            options,
            index => _codec.Compose(record.Scope, record.Id, index));
    }

    private IReadOnlyList<Segment> StructuralSegments(string content, int maxLength)
    {
        var segments = new List<Segment>();
        foreach (var (start, end) in Paragraphs(content))
        {
            var paragraph = content[start..end];
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                // Whitespace-only paragraphs carry no content and are dropped.
                continue;
            }

            if (ParagraphIsHeading(paragraph) || paragraph.Length <= maxLength)
            {
                segments.Add(new Segment(start, end));
                continue;
            }

            // Oversized paragraph: pack sentences, hard-slice over-max sentences.
            segments.AddRange(SentencePieces(paragraph, start, maxLength));
        }

        return segments;
    }

    private static IEnumerable<(int Start, int End)> Paragraphs(string content)
    {
        var start = 0;
        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                if (start < i)
                {
                    yield return (start, i);
                }

                start = i + 1;
            }
        }

        if (start < content.Length)
        {
            yield return (start, content.Length);
        }
    }

    // Pure line classification; intentionally static per architecture rule.
    private static bool ParagraphIsHeading(string paragraph)
    {
        return paragraph.TrimStart().StartsWith("#", StringComparison.Ordinal);
    }

    // Packs sentence spans (terminator-inclusive) of one paragraph into segments bounded
    // by maxSize; a single over-max sentence hard-slices into maxSize pieces.
    private IEnumerable<Segment> SentencePieces(string paragraph, int offset, int maxSize)
    {
        var pendingStart = -1;
        var pendingEnd = -1;
        foreach (var (relativeStart, relativeEnd) in Sentences(paragraph))
        {
            var length = relativeEnd - relativeStart;
            if (length > maxSize && pendingStart < 0)
            {
                for (var pos = relativeStart; pos < relativeEnd; pos += maxSize)
                {
                    yield return new Segment(offset + pos, offset + Math.Min(pos + maxSize, relativeEnd));
                }

                continue;
            }

            if (pendingStart >= 0 && relativeEnd - pendingStart > maxSize)
            {
                yield return new Segment(offset + pendingStart, offset + pendingEnd);
                pendingStart = -1;
            }

            if (pendingStart < 0)
            {
                pendingStart = relativeStart;
            }

            pendingEnd = relativeEnd;
        }

        if (pendingStart >= 0)
        {
            yield return new Segment(offset + pendingStart, offset + pendingEnd);
        }
    }

    // Splits paragraph text into sentence spans: terminator-inclusive, whitespace
    // between sentences excluded (whitespace-only gaps may be dropped at cut lines).
    private static IEnumerable<(int Start, int End)> Sentences(string paragraph)
    {
        var start = 0;
        var i = 0;
        while (i < paragraph.Length)
        {
            if (IsSentenceTerminator(paragraph[i]))
            {
                yield return (start, i + 1);
                var next = i + 1;
                while (next < paragraph.Length && char.IsWhiteSpace(paragraph[next]))
                {
                    next++;
                }

                start = next;
                i = next;
                continue;
            }

            i++;
        }

        if (start < paragraph.Length)
        {
            yield return (start, paragraph.Length);
        }
    }

    // Pure character classification; intentionally static per architecture rule.
    private static bool IsSentenceTerminator(char candidate)
    {
        return candidate is '.' or '!' or '?';
    }
}