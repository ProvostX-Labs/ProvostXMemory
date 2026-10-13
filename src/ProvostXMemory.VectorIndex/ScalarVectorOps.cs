using System;

namespace ProvostXMemory.VectorIndex;

/// <summary>
/// In-package scalar math for the FlatJson backend (instance-based, no static state).
/// Deliberately separate from the Engine package's SIMD VectorMath: the base package must
/// stay dependency-free below itself, and cosine/normalize here are O(dim) scalar loops —
/// the flat JSON backend's cost is dominated by I/O, not arithmetic.
/// </summary>
public sealed class ScalarVectorOps
{
    /// <summary>Euclidean norm with deterministic accumulation order.</summary>
    public float Norm(ReadOnlySpan<float> a)
    {
        var sum = 0.0f;
        foreach (var value in a)
        {
            sum += value * value;
        }

        return MathF.Sqrt(sum);
    }

    /// <summary>Copies then L2-normalizes; zero vectors stay all-zero.</summary>
    public float[] Normalize(ReadOnlySpan<float> a)
    {
        var copy = a.ToArray();
        var norm = Norm(copy);
        if (norm > float.Epsilon)
        {
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] /= norm;
            }
        }

        return copy;
    }

    /// <summary>Exact cosine = dot/(|a|·|b|); a zero vector yields 0.</summary>
    public float Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var na = Norm(a);
        var nb = Norm(b);
        if (na <= float.Epsilon || nb <= float.Epsilon)
        {
            return 0f;
        }

        var sum = 0.0f;
        for (var i = 0; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }

        return sum / (na * nb);
    }
}