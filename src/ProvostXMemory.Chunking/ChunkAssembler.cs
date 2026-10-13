using System;
using System.Collections.Generic;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Chunking;

/// <summary>
/// Shared chunk assembly used by the chunkers: turns sized segments into chunks,
/// enforcing the min/max clamps and computing verbatim offsets into the source. A
/// trailing chunk shorter than <see cref="ChunkOptions.MinChunkLength"/> merges into
/// its predecessor (may exceed the max — min is a hard floor, max a packing target).
/// Overlap is NOT applied here; chunkers bake it into their segmentation (window/stride).
/// Instances hold no state.
/// </summary>
internal sealed class ChunkAssembler
{
    // Pure geometry helper; stateless by design (architecture rule: no statics except pure functions).
    public static ChunkAssembler Instance { get; } = new();

    private ChunkAssembler()
    {
    }

    /// <summary>Parameter validation shared by all chunkers.</summary>
    public void Validate(MemoryRecord record, ChunkOptions options)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxChunkLength);
        ArgumentOutOfRangeException.ThrowIfNegative(options.Overlap);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MinChunkLength);
        if (options.Overlap >= options.MaxChunkLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options), "Overlap must be smaller than MaxChunkLength.");
        }

        if (options.MinChunkLength > options.MaxChunkLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options), "MinChunkLength must not exceed MaxChunkLength.");
        }
    }

    /// <summary>
    /// Assembles ordered, non-overlapping segments into chunks. Whitespace-only segments
    /// are dropped; every chunk's content equals
    /// <c>content[StartOffset..EndOffset]</c> and inherits the record's tags.
    /// </summary>
    public IReadOnlyList<Chunk> Assemble(
        string recordId,
        string content,
        IReadOnlyList<string>? tags,
        IReadOnlyList<Segment> segments,
        ChunkOptions options,
        Func<int, string> chunkIdFactory)
    {
        var chunks = new List<Chunk>();
        foreach (var segment in segments)
        {
            // Whitespace-only spans carry no information and are never chunked.
            if (string.IsNullOrWhiteSpace(content[segment.Start..segment.End]))
            {
                continue;
            }

            var start = segment.Start;
            var end = segment.End;
            if (chunks.Count > 0 && end - start < options.MinChunkLength)
            {
                // Trailing chunk below the floor merges into the predecessor.
                var previous = chunks[^1];
                start = Math.Min(previous.StartOffset, start);
                chunks[^1] = previous with
                {
                    Content = content[start..end],
                    EndOffset = end,
                };
                continue;
            }

            chunks.Add(new Chunk(
                chunkIdFactory(chunks.Count),
                recordId,
                content[start..end],
                Index: chunks.Count,
                Tags: tags,
                StartOffset: start,
                EndOffset: end));
        }

        return chunks;
    }
}

/// <summary>A half-open range [Start, End) cut from the source content.</summary>
internal readonly record struct Segment(int Start, int End)
{
    public int Length => End - Start;
}