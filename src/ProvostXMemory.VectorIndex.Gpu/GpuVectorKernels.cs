using ILGPU;
using ILGPU.Runtime;

namespace ProvostXMemory.VectorIndex.Gpu;

/// <summary>
/// ILGPU kernels: one thread per score slot, plain loops, no barriers/shared memory —
/// identical accumulation order on every backend (CPU simulator included), so GPU and
/// fallback results are bit-for-bit reproducible for the same inputs.
/// </summary>
public static class GpuVectorKernels
{
    /// <summary>Metric code for cosine similarity (matches GpuDistanceMetric.Cosine).</summary>
    public const int MetricCosine = (int)GpuDistanceMetric.Cosine;

    /// <summary>Scores matrix rows [rowOffset, rowOffset+index count) into scores[slot].</summary>
    public static void ScoreRowsKernel(
        Index1D index,
        ArrayView<float> query,
        ArrayView<float> rows,
        ArrayView<float> rowNorms,
        ArrayView<float> scores,
        int dim,
        int metric,
        float queryNorm,
        int rowOffset)
    {
        var row = rowOffset + index;
        if (row >= scores.Length)
        {
            return;
        }

        scores[row] = ScoreOne(query, rows, rowNorms, dim, metric, queryNorm, row, row);
    }

    /// <summary>
    /// Scores candidate rows listed memberRows[candidateOffset + index] into
    /// scores[candidateOffset + index] (slot-aligned with the posting entry).
    /// </summary>
    public static void ScoreIndexedRowsKernel(
        Index1D index,
        ArrayView<float> query,
        ArrayView<float> rows,
        ArrayView<float> rowNorms,
        ArrayView<float> scores,
        ArrayView<int> memberRows,
        int dim,
        int metric,
        float queryNorm,
        int candidateOffset)
    {
        var candidate = candidateOffset + index;
        if (candidate >= scores.Length)
        {
            return;
        }

        var matrixRow = memberRows[candidate];
        scores[candidate] = ScoreOne(query, rows, rowNorms, dim, metric, queryNorm, candidate, matrixRow);
    }

    private static float ScoreOne(
        ArrayView<float> query,
        ArrayView<float> rows,
        ArrayView<float> rowNorms,
        int dim,
        int metric,
        float queryNorm,
        int scoreSlot,
        int matrixRow)
    {
        var baseOffset = matrixRow * dim;
        var dot = 0f;
        for (var d = 0; d < dim; d++)
        {
            dot += query[d] * rows[baseOffset + d];
        }

        var rowNorm = rowNorms[matrixRow];
        if (metric == MetricCosine)
        {
            if (rowNorm <= float.Epsilon || queryNorm <= float.Epsilon)
            {
                return 0f;
            }

            return dot / (queryNorm * rowNorm);
        }

        // NegativeSquaredEuclidean: -(||q||^2 + ||r||^2 - 2 q·r); higher = closer.
        return (queryNorm * queryNorm) + (rowNorm * rowNorm) - (2f * dot);
    }
}