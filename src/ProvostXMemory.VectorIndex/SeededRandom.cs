using System;

namespace ProvostXMemory.VectorIndex;

/// <summary>
/// Pure deterministic pseudo-random generator (xorshift64*) used wherever the engine needs
/// seeded randomness (level assignment, quantizer seeding). Own implementation instead of
/// <see cref="Random"/> so results are stable across .NET versions and platforms.
/// Thread-unsafe by design: engine components own dedicated instances.
/// </summary>
public sealed class SeededRandom
{
    private ulong _state;

    public SeededRandom(int seed)
    {
        // Non-zero 64-bit state from an arbitrary int seed (splitmix-style scramble).
        _state = (ulong)seed * 0x9E3779B97F4A7C15UL ^ 0xD1342543DE82EF95UL;
        if (_state == 0)
        {
            _state = 0x2545F4914F6CDD1DUL;
        }

        for (var warmup = 0; warmup < 8; warmup++)
        {
            NextUInt64();
        }
    }

    /// <summary>Uniform in [0,1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>Uniform int in [minInclusive, maxExclusive); rejection-sampled, deterministic.</summary>
    public int Next(int minInclusive, int maxExclusive)
    {
        if (minInclusive > maxExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(minInclusive));
        }

        if (minInclusive == maxExclusive)
        {
            return minInclusive;
        }

        var range = (ulong)(maxExclusive - minInclusive);
        while (true)
        {
            var draw = NextUInt64();
            var limit = ulong.MaxValue - (ulong.MaxValue % range);
            if (draw <= limit)
            {
                return minInclusive + (int)(draw % range);
            }
        }
    }

    private ulong NextUInt64()
    {
        // xorshift64*: deterministic, full-period, tiny state.
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 0x2545F4914F6CDD1DUL;
    }
}