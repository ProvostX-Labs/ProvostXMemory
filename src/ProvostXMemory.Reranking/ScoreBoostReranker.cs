using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Reranking;

/// <summary>
/// Deterministic, zero-dependency reranker: additive boosts for tag/key/category matches
/// against the query's key terms, additive penalties for extremely short or long content.
/// Ties break by original rank (stable from the fused order); the output preserves input
/// casing of scores and never drops candidates.
/// </summary>
public sealed class ScoreBoostReranker : IReranker
{
    private const string RetrieverName = "rerank.score-boost";
    private const string OriginalFusedScoreStage = "fused";
    private const string RerankedScoreStage = "reranked";

    private readonly ScoreBoostRerankerOptions _options;

    /// <summary>Creates the reranker with injected options (defaults when null).</summary>
    public ScoreBoostReranker(ScoreBoostRerankerOptions? options = null)
    {
        _options = options ?? new ScoreBoostRerankerOptions();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ScoredMemoryRecord>> RerankAsync(
        RetrievalQuery query,
        IReadOnlyList<ScoredMemoryRecord> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidates);
        cancellationToken.ThrowIfCancellationRequested();

        // Pure scoring pass with original ranks; no I/O, deterministic. The static helper
        // is the architecture-sanctioned pure-function exception.
        var rescored = new List<(int OriginalRank, ScoredMemoryRecord Item)>(candidates.Count);
        for (var originalRank = 0; originalRank < candidates.Count; originalRank++)
        {
            var candidate = candidates[originalRank];
            ArgumentNullException.ThrowIfNull(candidate);
            rescored.Add((originalRank, Boost(query, candidate, originalRank, _options)));
        }

        var ordered = rescored
            .OrderByDescending(entry => entry.Item.Score)
            .ThenBy(entry => entry.OriginalRank)
            .Select(entry => entry.Item)
            .ToList();

        IReadOnlyList<ScoredMemoryRecord> result = ordered;
        return Task.FromResult(result);
    }

    // Pure candidate → rescored mapping; deterministic; intentional static per architecture rule.
    private static ScoredMemoryRecord Boost(
        RetrievalQuery query,
        ScoredMemoryRecord candidate,
        int originalRank,
        ScoreBoostRerankerOptions options)
    {
        var boost = 0.0;
        var record = candidate.Record;
        var content = record.Content ?? string.Empty;

        if (record.Tags.Count > 0)
        {
            foreach (var term in QueryTokens.KeyTerms(query.Text))
            {
                foreach (var tag in record.Tags)
                {
                    if (string.Equals(tag, term, StringComparison.OrdinalIgnoreCase))
                    {
                        boost += options.TagMatchBoost;
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(record.Key))
        {
            var keyTerms = QueryTokens.KeyTerms(record.Key);
            foreach (var term in QueryTokens.KeyTerms(query.Text))
            {
                if (keyTerms.Contains(term, StringComparer.OrdinalIgnoreCase))
                {
                    boost += options.KeyTermBoost;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(record.Category)
            && !string.IsNullOrWhiteSpace(query.Text))
        {
            foreach (var term in QueryTokens.KeyTerms(query.Text))
            {
                if (string.Equals(record.Category, term, StringComparison.OrdinalIgnoreCase))
                {
                    boost += options.CategoryBoost;
                }
            }
        }

        if (content.Length < options.MinLength)
        {
            boost -= options.ShortLengthPenalty;
        }
        else if (content.Length > options.MaxLength)
        {
            boost -= options.LongLengthPenalty;
        }

        var rerankedScore = candidate.Score + boost;
        var stageScores = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [OriginalFusedScoreStage] = candidate.Score,
            [RerankedScoreStage] = rerankedScore,
        };

        return new ScoredMemoryRecord(
            record,
            rerankedScore,
            new RetrievalProvenance(
                $"{RetrieverName}:{originalRank}",
                stageScores));
    }
}