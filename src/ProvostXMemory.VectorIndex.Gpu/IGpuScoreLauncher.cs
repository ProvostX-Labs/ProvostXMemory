using System;

using ILGPU;
using ILGPU.Runtime;

namespace ProvostXMemory.VectorIndex.Gpu;

/// <summary>
/// Compiled-kernel launcher boundary: hides ILGPU's launcher delegates behind an
/// injectable abstraction so index classes stay backend-agnostic and testable.
/// </summary>
public interface IGpuScoreLauncher
{
    /// <summary>Launches the matrix-row scoring kernel over rows [rowOffset, rowEnd).</summary>
    void ScoreRows(
        ArrayView<float> query,
        ArrayView<float> rows,
        ArrayView<float> rowNorms,
        ArrayView<float> scores,
        int dim,
        int metric,
        float queryNorm,
        int rowOffset,
        int rowEnd);

    /// <summary>
    /// Launches the posting-member scoring kernel: scores rows memberRows[j] into
    /// scores[j] for j in [candidateOffset, candidateEnd).
    /// </summary>
    void ScoreIndexedRows(
        ArrayView<float> query,
        ArrayView<float> rows,
        ArrayView<float> rowNorms,
        ArrayView<float> scores,
        ArrayView<int> memberRows,
        int dim,
        int metric,
        float queryNorm,
        int candidateOffset,
        int candidateEnd);
}