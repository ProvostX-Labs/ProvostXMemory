using System;

namespace ProvostXMemory.VectorIndex.Engine;

/// <summary>
/// HNSW backend tuning parameters, mirroring the canonical algorithm's knobs:
/// <see cref="M"/> outgoing links per node above layer 0 (<see cref="MaxLevel0Connections"/>
/// on layer 0, typically 2·M), <see cref="LevelFactor"/> as the level-assignment divisor
/// (1/ln(M) canonically), <see cref="EfConstruction"/> beam width while inserting,
/// <see cref="EfSearch"/> beam width at query time, and the deterministic
/// <see cref="Seed"/> for level assignment. Same seed + same insertion order → same graph.
/// </summary>
public sealed record HnswOptions(
    int M,
    int MaxLevel0Connections,
    double LevelFactor,
    int EfConstruction,
    int EfSearch,
    int Seed)
{
    public HnswOptions()
        : this(M: 16, MaxLevel0Connections: 32, LevelFactor: 1.0 / Math.Log(16.0), EfConstruction: 200, EfSearch: 96, Seed: 1729)
    {
    }

    public static HnswOptions Default { get; } = new();

    /// <summary>Adds <paramref name="minEf"/> to a requested beam width (ef ≥ k convention).</summary>
    public int EffectiveEf(int requestedEf, int minEf) => Math.Max(requestedEf, minEf);
}