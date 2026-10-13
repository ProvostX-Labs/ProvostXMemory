using System;

namespace ProvostXMemory.VectorIndex.Engine;

/// <summary>
/// Reusable traversal context for one built HNSW graph: row-order ids parallel to
/// normalized vector rows, a per-query dot-product score cache (NaN = unscored; zero-vector
/// queries legitimately score 0 and MUST be rescored), and the visited set shared by
/// construction-time multi-layer traversals. Score cache + visited set are reset per query
/// (<see cref="ResetForNewQuery"/>); the query is copied into a reusable buffer so no span
/// state is held across calls.
/// </summary>
public sealed class HnswSearchContext
{
    private float[] _query = Array.Empty<float>();

    public HnswSearchContext(string[] ids, float[][] normalizedRows)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(normalizedRows);
        if (ids.Length != normalizedRows.Length)
        {
            throw new ArgumentException("Ids and rows must be parallel arrays of equal length.");
        }

        Ids = ids;
        NormalizedRows = normalizedRows;
        ScoreOfNode = new float[normalizedRows.Length];
        Array.Fill(ScoreOfNode, float.NaN);
        Visited = new HashSet<int>();
    }

    public string[] Ids { get; }

    public float[][] NormalizedRows { get; }

    public float[] ScoreOfNode { get; }

    public HashSet<int> Visited { get; }

    /// <summary>Score of a node under the current query (NaN until first scored).</summary>
    public float Score(int node)
    {
        var score = ScoreOfNode[node];
        if (float.IsNaN(score))
        {
            score = VectorMath.Dot(_query, NormalizedRows[node]);
            ScoreOfNode[node] = score;
        }

        return score;
    }

    /// <summary>Copies the normalized query into the reusable buffer.</summary>
    public void SetQuery(ReadOnlySpan<float> normalizedQuery)
    {
        if (_query.Length != normalizedQuery.Length)
        {
            _query = new float[normalizedQuery.Length];
        }

        normalizedQuery.CopyTo(_query);
    }

    /// <summary>Wipes score cache (to NaN) + visited set for a fresh search.</summary>
    public void ResetForNewQuery()
    {
        Array.Fill(ScoreOfNode, float.NaN);
        Visited.Clear();
    }
}