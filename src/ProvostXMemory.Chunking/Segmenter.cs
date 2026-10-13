using System;
using System.Collections.Generic;

namespace ProvostXMemory.Chunking;

/// <summary>
/// Text segmentation engine shared by the chunkers: cuts content into ordered,
/// non-overlapping segments under the max-length clamp. Preference order: paragraph
/// breaks (".."), single newlines, sentence boundaries (terminator + separator
/// whitespace; the terminator stays with the slice, the whitespace is consumed after),
/// finally hard slices of exactly maxLength. Instances hold no state.
/// </summary>
internal sealed class Segmenter
{
    // Pure cutting geometry; stateless helper instance (architecture rule: no shared mutable state).
    public static Segmenter Instance { get; } = new();

    private const char Newline = '\n';
    private const char Space = ' ';
    private const char Tab = '\t';
    private const char CarriageReturn = '\r';

    private Segmenter()
    {
    }

    /// <summary>
    /// Segments <paramref name="content"/> into max-length-bounded slices. The character
    /// at a cut position (newline, separator whitespace after a sentence terminator)
    /// belongs to no slice; consecutive separators are consumed together, so no slice is
    /// empty and no non-separator character is lost.
    /// </summary>
    public IReadOnlyList<Segment> Segments(string content, int maxLength)
    {
        var result = new List<Segment>();
        if (string.IsNullOrEmpty(content))
        {
            return result;
        }

        var position = 0;
        while (position < content.Length)
        {
            if (content.Length - position <= maxLength)
            {
                result.Add(new Segment(position, content.Length));
                break;
            }

            var boundary = FindBoundary(content, position, maxLength);
            result.Add(new Segment(position, boundary));
            if (boundary < content.Length && IsSeparatorWhitespace(content[boundary]))
            {
                // Separator char at the cut is consumed (plus consecutive separators).
                position = boundary + 1;
                while (position < content.Length && IsSeparatorWhitespace(content[position]))
                {
                    position++;
                }
            }
            else
            {
                // Hard cut: the char at the cut is real text and starts the next slice.
                position = boundary;
            }
        }

        return result;
    }

    private int FindBoundary(string content, int position, int maxLength)
    {
        var end = position + maxLength;

        // 1) Largest separators first: paragraph breaks ("..") then single newlines.
        for (var scan = Math.Min(end, content.Length - 1); scan > position; scan--)
        {
            if (content[scan] == Newline && content[scan - 1] == Newline)
            {
                return scan;
            }
        }

        for (var scan = Math.Min(end, content.Length - 1); scan > position; scan--)
        {
            if (content[scan] == Newline)
            {
                return scan;
            }
        }

        // 2) Sentence boundaries: terminator followed by separator whitespace. The cut
        // sits after the terminator; the whitespace is consumed by the caller.
        for (var scan = Math.Min(end, content.Length - 1); scan > position; scan--)
        {
            if (IsSentenceTerminator(content[scan])
                && scan + 1 < content.Length
                && IsSeparatorWhitespace(content[scan + 1]))
            {
                return scan + 1;
            }
        }

        // 3) Give up: slice at exactly maxLength, but never between CR and LF.
        if (end < content.Length
            && content[end] == Newline
            && content[end - 1] == CarriageReturn)
        {
            end--;
        }

        return end;
    }

    // Pure character classification; intentionally static per architecture rule.
    private static bool IsSentenceTerminator(char candidate)
    {
        return candidate is '.' or '!' or '?';
    }

    // Pure character classification; intentionally static per architecture rule.
    private static bool IsSeparatorWhitespace(char candidate)
    {
        return candidate is Newline or Space or Tab or CarriageReturn;
    }

    /// <summary>Cuts whitespace-separated words of <paramref name="text"/> together with
    /// the half-open range each word occupies in the source (exact word-level packing).</summary>
    public IReadOnlyList<WordSpan> Words(string text)
    {
        var words = new List<WordSpan>();
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var atEnd = i == text.Length;
            if (!atEnd && !char.IsWhiteSpace(text[i]))
            {
                if (start < 0)
                {
                    start = i;
                }

                continue;
            }

            if (start >= 0)
            {
                words.Add(new WordSpan(text[start..i], start, i));
                start = -1;
            }
        }

        return words;
    }
}

/// <summary>A word and the half-open range it occupies in the source text.</summary>
internal readonly record struct WordSpan(string Text, int Start, int End);