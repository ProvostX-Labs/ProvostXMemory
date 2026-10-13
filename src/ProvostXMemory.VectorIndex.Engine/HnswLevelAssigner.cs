using System;

namespace ProvostXMemory.VectorIndex.Engine;

/// <summary>
/// Deterministic level assignment for HNSW layers: each node's top level is drawn from the
/// geometric distribution ⌊-ln(U)/ln(levelFactor)⌋ (U uniform in [0,1), factor &lt; 1). Own
/// <see cref="SeededRandom"/> keeps level assignment stable across .NET versions/platforms; same
/// seed + same insertion order → identical layer structure.
/// </summary>
public sealed class HnswLevelAssigner
{
    private readonly double _levelLogFactor;
    private readonly SeededRandom _random;

    public HnswLevelAssigner(double levelFactor, int seed)
    {
        if (levelFactor is <= 0.0 or >= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(levelFactor), levelFactor, "Level factor must be in (0, 1).");
        }

        LevelFactor = levelFactor;
        _levelLogFactor = -Math.Log(levelFactor);
        _random = new SeededRandom(seed);    }

    public double LevelFactor { get; }

    /// <summary>Draws the top layer of the next inserted node (0 for the entry insert).</summary>
    public int NextTopLevel() => (int)(-Math.Log(1.0 - _random.NextDouble()) / _levelLogFactor);
}