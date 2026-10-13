using System;
using System.Collections.Generic;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Fusion;


/// <summary>Fuses multiple ranked lists (RRF, k=60 default, weighted, deterministic ties).</summary>
public interface IResultFuser
{
    /// <summary>
    /// Fuses <paramref name="rankedLists"/> into one ranked list. Each item scores
    /// sum(weight / (k + rank)) with rank starting at 1; per-stage weights come from
    /// <paramref name="options"/> ("dense"/"lexical" source names; others weigh 1.0).
    /// </summary>
    IReadOnlyList<ScoredId> Fuse(IReadOnlyList<RankedList> rankedLists, FusionOptions options);
}