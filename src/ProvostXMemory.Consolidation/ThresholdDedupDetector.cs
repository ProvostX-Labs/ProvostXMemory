using ProvostXMemory.Abstractions;
using ProvostXMemory.VectorIndex;

namespace ProvostXMemory.Consolidation;

/// <summary>
/// Near-duplicate detection: cosine over records embedded via the injected
/// <see cref="IVectorEmbedder"/>, falling back to lexical Jaccard over token sets when
/// both vectors degenerate (zero norm). A pair counts as duplicate when its similarity
/// is greater than or equal to the given threshold. Arguments are ordered
/// deterministically (earlier-id first) before evaluation, so the result never depends
/// on call-site order; evaluation itself is pure. This type holds no mutable state.
/// </summary>
public sealed class ThresholdDedupDetector : IDedupDetector
{
    /// <summary>Lexical Jaccard similarity above which a pair counts as duplicate when vectors are unavailable.</summary>
    public const double LexicalFallbackThreshold = 0.9;

    private readonly IVectorEmbedder _embedder;

    public ThresholdDedupDetector(IVectorEmbedder embedder)
    {
        ArgumentNullException.ThrowIfNull(embedder);
        _embedder = embedder;
    }

    public async Task<bool> IsDuplicateAsync(MemoryRecord left, MemoryRecord right, double threshold, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ArgumentException.ThrowIfNullOrWhiteSpace(left.Id);

        if (threshold is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Threshold must be within [0, 1].");
        }

        // Deterministic pair ordering: always evaluate (lower id, higher id) so callers
        // may pass the pair in any order. Pure comparison, no shared state.
        (MemoryRecord first, MemoryRecord second) = string.CompareOrdinal(left.Id, right.Id) <= 0
            ? (left, right)
            : (right, left);

        if (string.Equals(first.Id, second.Id, StringComparison.Ordinal))
        {
            return true; // a record is trivially a duplicate of itself
        }

        var firstVector = await _embedder.EmbedAsync(first.Content, cancellationToken).ConfigureAwait(false);
        var secondVector = await _embedder.EmbedAsync(second.Content, cancellationToken).ConfigureAwait(false);

        if (firstVector.Length == 0 || firstVector.Length != secondVector.Length)
        {
            return LexicalSimilarity(first.Content, second.Content) >= threshold;
        }

        var cosine = new ScalarVectorOps().Cosine(firstVector, secondVector);
        if (cosine <= float.Epsilon)
        {
            // Degenerate vectors (zero norm or orthogonal-by-embedding) → lexical fallback.
            return LexicalSimilarity(first.Content, second.Content) >= threshold;
        }

        return cosine >= threshold;
    }

    /// <summary>Lexical Jaccard over lower-cased letter/digit token sets.</summary>
    private static double LexicalSimilarity(string first, string second)
    {
        var firstTokens = TokenSet(first);
        var secondTokens = TokenSet(second);
        if (firstTokens.Count == 0 && secondTokens.Count == 0)
        {
            return 1.0; // two empty texts are considered identical
        }

        var intersection = 0;
        if (firstTokens.Overlaps(secondTokens))
        {
            foreach (var token in secondTokens)
            {
                if (firstTokens.Contains(token))
                {
                    intersection++;
                }
            }
        }

        var union = firstTokens.Count + secondTokens.Count - intersection;
        return union == 0 ? 1.0 : (double)intersection / union;
    }

    private static HashSet<string> TokenSet(string text)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var isWord = i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '\'');
            if (isWord && start < 0)
            {
                start = i;
            }
            else if (!isWord && start >= 0)
            {
                tokens.Add(text[start..i].ToLowerInvariant());
                start = -1;
            }
        }

        return tokens;
    }
}