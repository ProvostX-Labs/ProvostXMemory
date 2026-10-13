using ProvostXMemory.Abstractions;
using ProvostXMemory.Consolidation;
using ProvostXMemory.ConsolidationTests;

using Xunit;

namespace ProvostXMemory.ConsolidationTests;

/// <summary>ThresholdDedupDetector: cosine via fake embedder, Jaccard fallback, deterministic pair order.</summary>
public sealed class ThresholdDedupDetectorTests
{
    private const double Precision = 1e-6;

    private static ThresholdDedupDetector CreateDetector() => new(new HashFakeEmbedder());

    private static MemoryRecord Record(string id, string content)
        => TestRecords.Create(id, id, content);

    [Fact]
    public async Task Identical_text_is_duplicate_at_any_threshold()
    {
        var detector = CreateDetector();
        var left = Record("a", "alpha bravo charlie");
        var right = Record("b", "alpha bravo charlie");
        Assert.True(await detector.IsDuplicateAsync(left, right, 0.95));
    }

    [Fact]
    public async Task Disjoint_text_is_not_duplicate()
    {
        var detector = CreateDetector();
        var left = Record("a", "alpha bravo");
        var right = Record("b", "xray yankee");
        Assert.False(await detector.IsDuplicateAsync(left, right, 0.5));
    }

    [Fact]
    public async Task Cosine_threshold_gate_matches_expected_similarity()
    {
        // "alpha bravo" vs "alpha bravo charlie": cosine = 2/sqrt(2*3) ≈ 0.8165.
        var detector = CreateDetector();
        var left = Record("a", "alpha bravo");
        var right = Record("b", "alpha bravo charlie");
        var expected = 2.0 / Math.Sqrt(6.0);
        Assert.True(await detector.IsDuplicateAsync(left, right, expected - Precision));
        Assert.False(await detector.IsDuplicateAsync(left, right, expected + Precision));
    }

    [Fact]
    public async Task Same_record_ids_always_duplicate()
    {
        var detector = CreateDetector();
        var left = Record("a", "alpha bravo");
        var right = Record("a", "totally different words");
        Assert.True(await detector.IsDuplicateAsync(left, right, 0.95));
    }

    [Fact]
    public async Task Pair_argument_order_does_not_change_result()
    {
        var detector = CreateDetector();
        var first = Record("a", "alpha bravo");
        var second = Record("b", "alpha bravo charlie");
        var expected = 2.0 / Math.Sqrt(6.0);
        var threshold = expected - Precision;

        // Both orderings must agree (deterministic pair ordering inside the detector).
        Assert.True(await detector.IsDuplicateAsync(first, second, threshold));
        Assert.True(await detector.IsDuplicateAsync(second, first, threshold));
        Assert.False(await detector.IsDuplicateAsync(first, second, expected + Precision));
        Assert.False(await detector.IsDuplicateAsync(second, first, expected + Precision));
    }

    [Fact]
    public async Task Degenerate_vectors_fall_back_to_lexical_jaccard()
    {
        // Punctuation-only content produces NO tokens → zero-norm vectors → cosine 0 →
        // lexical fallback. Two empty token sets are defined as identical (Jaccard 1.0).
        var detector = CreateDetector();
        var punctuation = Record("a", "...");
        var punctuationCopy = Record("b", "...");
        var otherPunctuation = Record("c", "!!!");
        var worded = Record("d", "alpha bravo");

        Assert.True(await detector.IsDuplicateAsync(punctuation, punctuationCopy, 0.95));
        Assert.True(await detector.IsDuplicateAsync(punctuation, otherPunctuation, 0.95));
        Assert.False(await detector.IsDuplicateAsync(punctuation, worded, 0.1));
    }

    [Fact]
    public async Task Vector_path_and_fallback_path_are_consistent_for_same_text()
    {
        var detector = CreateDetector();

        // Non-degenerate: identical digit tokens → identical vectors → cosine 1.
        Assert.True(await detector.IsDuplicateAsync(Record("a", "7 7 7"), Record("b", "7 7 7"), 0.99));

        // Degenerate vs non-degenerate mix falls back to Jaccard: empty vs non-empty → 0.
        Assert.False(await detector.IsDuplicateAsync(Record("c", "..."), Record("d", "alpha"), 0.95));
    }

    [Fact]
    public async Task Invalid_threshold_is_rejected()
    {
        var detector = CreateDetector();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => detector.IsDuplicateAsync(Record("a", "alpha"), Record("b", "alpha"), 1.5));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => detector.IsDuplicateAsync(Record("a", "alpha"), Record("b", "alpha"), -0.1));
    }
}