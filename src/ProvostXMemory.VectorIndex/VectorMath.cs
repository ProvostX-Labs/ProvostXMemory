using System;
using System.Runtime.Intrinsics;

namespace ProvostXMemory.VectorIndex;

/// <summary>
/// PURE STATIC UTILITY (no mutable state, thread-safe by construction; the one sanctioned
/// static exception per architecture principles). Stateless numeric kernels for the engine:
/// dot product via Vector512 → Vector256 → Vector128 with scalar fallback, norm, normalize,
/// cosine. Deterministic order of accumulation for a given hardware width.
/// </summary>
public static class VectorMath
{
    /// <summary>Dot product of equal-length spans; best available SIMD width, scalar fallback.</summary>
    public static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Dot product requires equal-length spans.");
        }

        if (Vector512.IsHardwareAccelerated && a.Length >= Vector512<float>.Count)
        {
            var acc = Vector512<float>.Zero;
            var i = 0;
            var limit = a.Length - (a.Length % Vector512<float>.Count);
            while (i < limit)
            {
                acc += Vector512.Multiply(
                    Vector512.Create(a.Slice(i, Vector512<float>.Count)),
                    Vector512.Create(b.Slice(i, Vector512<float>.Count)));
                i += Vector512<float>.Count;
            }

            var sum = Vector512.Sum(acc);
            while (i < a.Length)
            {
                sum += a[i] * b[i];
                i++;
            }

            return sum;
        }

        if (Vector256.IsHardwareAccelerated && a.Length >= Vector256<float>.Count)
        {
            var acc = Vector256<float>.Zero;
            var i = 0;
            var limit = a.Length - (a.Length % Vector256<float>.Count);
            while (i < limit)
            {
                acc += Vector256.Multiply(
                    Vector256.Create(a.Slice(i, Vector256<float>.Count)),
                    Vector256.Create(b.Slice(i, Vector256<float>.Count)));
                i += Vector256<float>.Count;
            }

            var sum = Vector256.Dot(acc, Vector256<float>.One);
            while (i < a.Length)
            {
                sum += a[i] * b[i];
                i++;
            }

            return sum;
        }

        if (Vector128.IsHardwareAccelerated && a.Length >= Vector128<float>.Count)
        {
            var acc = Vector128<float>.Zero;
            var i = 0;
            var limit = a.Length - (a.Length % Vector128<float>.Count);
            while (i < limit)
            {
                acc += Vector128.Multiply(
                    Vector128.Create(a.Slice(i, Vector128<float>.Count)),
                    Vector128.Create(b.Slice(i, Vector128<float>.Count)));
                i += Vector128<float>.Count;
            }

            var sum = Vector128.Dot(acc, Vector128<float>.One);
            while (i < a.Length)
            {
                sum += a[i] * b[i];
                i++;
            }

            return sum;
        }

        return DotScalar(a, b);
    }

    /// <summary>Euclidean norm; scalar loop with deterministic accumulation order.</summary>
    public static float Norm(ReadOnlySpan<float> a)
    {
        var sum = 0.0f;
        foreach (var value in a)
        {
            sum += value * value;
        }

        return MathF.Sqrt(sum);
    }

    /// <summary>L2-normalizes in place; zero vectors are left as all-zero. Returns the same span.</summary>
    public static Span<float> NormalizeInPlace(Span<float> a)
    {
        var norm = Norm(a);
        if (norm > float.Epsilon)
        {
            for (var i = 0; i < a.Length; i++)
            {
                a[i] /= norm;
            }
        }

        return a;
    }

    /// <summary>Copies then L2-normalizes; zero vectors stay all-zero.</summary>
    public static float[] Normalize(ReadOnlySpan<float> a)
    {
        var copy = a.ToArray();
        NormalizeInPlace(copy);
        return copy;
    }

    /// <summary>Exact cosine similarity = dot(a,b)/(|a|·|b|); a zero vector yields 0.</summary>
    public static float Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var na = Norm(a);
        var nb = Norm(b);
        if (na <= float.Epsilon || nb <= float.Epsilon)
        {
            return 0f;
        }

        return Dot(a, b) / (na * nb);
    }

    private static float DotScalar(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var sum = 0.0f;
        for (var i = 0; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }
}