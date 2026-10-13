using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Consolidation;

/// <summary>
/// Consolidation pass tuning knobs, layered over <see cref="ConsolidationOptions"/>
/// (thresholds have defaults and stay host-overridable via the base record).
/// </summary>
public sealed record ConsolidationTuning
{
    /// <summary>Lexical Jaccard fallback used when vector similarity degenerates.</summary>
    public double LexicalFallbackThreshold { get; init; } = ThresholdDedupDetector.LexicalFallbackThreshold;

    /// <summary>Cosine similarity above which records join the same near-duplicate cluster.</summary>
    public double ClusterJoinThreshold { get; init; } = 0.86;

    /// <summary>Minimum cluster size before a summary record is emitted.</summary>
    public int SummarizeMinClusterSize { get; init; } = 3;

    /// <summary>Maximum cluster members folded into one summary (larger clusters are truncated deterministically).</summary>
    public int SummaryExcerptLimit { get; init; } = 4;

    /// <summary>Excerpt length in characters taken from each representative record.</summary>
    public int SummaryExcerptLength { get; init; } = 120;
}