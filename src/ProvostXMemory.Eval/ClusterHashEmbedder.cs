using System;
using System.Collections.Generic;
using System.Globalization;

using ProvostXMemory.VectorIndex;

namespace ProvostXMemory.Eval;

/// <summary>
/// Deterministic fake embedder for eval runs — NOT a real model. Cluster-preserving:
/// every token maps to a fixed slot from a stable FNV-1a hash; each of a record's tokens
/// adds a fixed cluster id + token hash signature so similar content (shared cluster
/// terms) lands near the same direction, with per-record seeded noise amplitude only.
/// No randomness at embed time: same text → same vector, always.
/// </summary>
public sealed class ClusterHashEmbedder : IVectorEmbedder
{
    private readonly int _dim;
    private readonly float _noiseAmplitude;

    /// <summary>Dim is validated; slot hashing uses the instance dim.</summary>
    public ClusterHashEmbedder(int dim = 128, float noiseAmplitude = 0.05f)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dim, 16);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dim, 4096);
        if (noiseAmplitude is < 0.0f or > 0.25f)
        {
            throw new ArgumentOutOfRangeException(nameof(noiseAmplitude));
        }

        _dim = dim;
        _noiseAmplitude = noiseAmplitude;
    }

    public string ModelHash => $"cluster-hash-v1-d{_dim}";

    public int Dim => _dim;

    public Task<float[]> EmbedAsync(string text, System.Threading.CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();

        var vector = new float[_dim];
        var noise = new float[_dim];
        var tokenCount = 0;
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var isWord = i < text.Length && char.IsLetterOrDigit(text[i]);
            if (isWord && start < 0)
            {
                start = i;
            }
            else if (!isWord && start >= 0)
            {
                    var token = text[start..i].ToLowerInvariant();
                if (KnownClusters.TryGetValue(token, out var cluster))
                {
                    // Cluster-preserving signal: cluster anchor + token identity.
                    vector[Slot("cluster:" + cluster, _dim)] += 1f;
                    vector[Slot(token, _dim)] += 1f;
                }
                else
                {
                    // Unknown token (filler/noise/hard-negative): small seeded jitter into
                    // noise buffer; weighted by a stable token hash for deterministic spread.
                    var jitter = StableFloat(StableHash(token), -1f, 1f);
                    noise[Slot("noise", _dim)] += jitter;
                    noise[Slot(token, _dim)] += jitter * 0.5f;
                }

                tokenCount++;
                start = -1;
            }
        }

        // Scale noise with token count so it perturbs but never dominates the signal.
        if (tokenCount > 0)
        {
            var scale = Math.Min(1f, _noiseAmplitude * tokenCount / 10f);
            for (var i = 0; i < _dim; i++)
            {
                vector[i] += noise[i] * scale;
            }
        }

        return Task.FromResult(vector);
    }

    /// <summary>Cluster name lookup for content tokens (eval-only knowledge).</summary>
    private static readonly System.Collections.Generic.IReadOnlyDictionary<string, string> KnownClusters =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kubernetes"] = "kubernetes",
            ["helm"] = "kubernetes",
            ["postgres"] = "postgres",
            ["vacuum"] = "postgres",
            ["invoice"] = "billing",
            ["stripe"] = "billing",
            ["passport"] = "travel",
            ["visa"] = "travel",
            ["sourdough"] = "baking",
            ["banneton"] = "baking",
            ["telescope"] = "astronomy",
            ["jupiter"] = "astronomy",
            ["docker"] = "docker",
            ["compose"] = "docker",
            ["chopin"] = "piano",
            ["nocturne"] = "piano",
        };

    // Stable slot mapping (FNV-1a 64, mod dim); pure; intentionally static per architecture rule.
    private static int Slot(string token, int dim)
    {
        var hash = 0xcbf29ce484222325UL;
        foreach (var ch in token)
        {
            hash ^= ch;
            hash *= 0x100000001b3UL;
        }

        return (int)(hash % (ulong)dim);
    }

    // Stable FNV-1a 64 token hash (string.GetHashCode is process-randomized); pure; static.
    private static ulong StableHash(string token)
    {
        var hash = 0xcbf29ce484222325UL;
        foreach (var ch in token)
        {
            hash ^= ch;
            hash *= 0x100000001b3UL;
        }

        return hash;
    }

    // Deterministic pseudo-float in [min, max) from a 64-bit seed; pure; intentionally static.
    private static float StableFloat(ulong seed, float min, float max)
    {
        var unit = (uint)(seed * 0x9E3779B97F4A7C15UL >> 40); // splitmix-style top bits → [0, 2^24)
        var normalized = unit / (float)(1 << 24);
        return min + normalized * (max - min);
    }
}