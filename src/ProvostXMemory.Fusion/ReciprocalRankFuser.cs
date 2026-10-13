using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Fusion;

/// <summary>
/// Reciprocal Rank Fusion: each item scores sum over lists of weight / (k + rank),
/// rank starting at 1. Default k = 60. Stage sources ("dense", "lexical") use the
/// stage weights from <see cref="FusionOptions"/>; unknown sources weigh 1.0. Ties break
/// deterministically by id (ordinal) then by best (lowest) rank across lists.
/// </summary>
public sealed class ReciprocalRankFuser : IResultFuser
{
    // "dense" and "lexical" match the hybrid retrieval stage names from ARCHITECTURE.md.
    private const string DenseSource = "dense";
    private const string LexicalSource = "lexical";
    private const double UnlistedSourceWeight = 1.0;

    /// <inheritdoc />
    public IReadOnlyList<ScoredId> Fuse(IReadOnlyList<RankedList> rankedLists, FusionOptions options)
    {
        ArgumentNullException.ThrowIfNull(rankedLists);
        ArgumentNullException.ThrowIfNull(options);

        if (rankedLists.Count == 0)
        {
            return Array.Empty<ScoredId>();
        }

        var rrfK = options.RrfK;

        var fusedScores = new Dictionary<string, double>(StringComparer.Ordinal);
        var bestRanks = new Dictionary<string, int>(StringComparer.Ordinal);
        var firstHitById = new Dictionary<string, ScoredId>(StringComparer.Ordinal);

        foreach (var list in rankedLists)
        {
            ArgumentNullException.ThrowIfNull(list);
            var weight = ResolveWeight(list.Source, options);
            var rank = 1;
            foreach (var item in list.Items)
            {
                var contribution = weight / (rrfK + rank);
                fusedScores[item.Id] = fusedScores.TryGetValue(item.Id, out var existing)
                    ? existing + contribution
                    : contribution;

                if (!bestRanks.TryGetValue(item.Id, out var currentBest) || rank < currentBest)
                {
                    bestRanks[item.Id] = rank;
                }

                // First list containing the id wins provenance (deterministic iteration order).
                if (!firstHitById.ContainsKey(item.Id))
                {
                    firstHitById[item.Id] = item;
                }

                rank++;
            }
        }

        return fusedScores
            .Select(pair => new FusedHit(
                Id: pair.Key,
                RrfScore: pair.Value,
                BestRank: bestRanks[pair.Key],
                ScopeKind: firstHitById[pair.Key].ScopeKind,
                ScopeId: firstHitById[pair.Key].ScopeId,
                RawScore: firstHitById[pair.Key].Score))
            .OrderByDescending(f => f.RrfScore)
            .ThenBy(f => f.Id, StringComparer.Ordinal)
            .ThenBy(f => f.BestRank)
            .Select(f => new ScoredId(f.ScopeKind, f.ScopeId, f.Id, f.RrfScore))
            .ToList();
    }

    // Pure source→weight mapping; intentionally static per architecture rule.
    private static double ResolveWeight(string source, FusionOptions options)
    {
        return source switch
        {
            DenseSource => options.DenseWeight,
            LexicalSource => options.LexicalWeight,
            _ => UnlistedSourceWeight,
        };
    }

    private readonly record struct FusedHit(
        string Id,
        double RrfScore,
        int BestRank,
        string ScopeKind,
        string ScopeId,
        double RawScore);
}