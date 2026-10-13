using System;
using System.Collections.Generic;

namespace ProvostXMemory.VectorIndex;


/// <summary>Inverted lists: cluster id → member rows + centroid access for probing.</summary>
public sealed class IvfPostingLists
{
    private readonly float[][] _centroids;
    private readonly string[] _idOfMember;
    private readonly float[][] _vectorOfMember;
    private readonly List<int>[] _memberIndicesPerCluster;

    /// <summary>Members in row order; each pre-assigned to a cluster by the quantizer.</summary>
    public IvfPostingLists(float[][] centroids, IReadOnlyList<IvfMember> members)
    {
        ArgumentNullException.ThrowIfNull(centroids);
        ArgumentNullException.ThrowIfNull(members);
        _centroids = centroids;
        _idOfMember = new string[members.Count];
        _vectorOfMember = new float[members.Count][];
        _memberIndicesPerCluster = new List<int>[centroids.Length];
        for (var c = 0; c < centroids.Length; c++)
        {
            _memberIndicesPerCluster[c] = new List<int>();
        }

        for (var m = 0; m < members.Count; m++)
        {
            var member = members[m];
            if (member.Cluster < 0 || member.Cluster >= centroids.Length)
            {
                throw new ArgumentException(
                    $"Member cluster {member.Cluster} out of range [0, {centroids.Length}).");
            }

            _idOfMember[m] = member.Id;
            _vectorOfMember[m] = member.Vector;
            _memberIndicesPerCluster[member.Cluster].Add(m);
        }
    }

    public int ClusterCount => _centroids.Length;

    public int MemberCount => _idOfMember.Length;

    public float[] CentroidOf(int cluster) => _centroids[cluster];

    /// <summary>Scores centroids against the query (higher = better); deterministic tie-break by cluster id.</summary>
    public List<(int Cluster, float Score)> ProbeOrder(ReadOnlySpan<float> query, VectorScanner scanner)
    {
        var scored = new List<(int Cluster, float Score)>(_centroids.Length);
        for (var c = 0; c < _centroids.Length; c++)
        {
            scored.Add((c, scanner.Score(query, _centroids[c])));
        }

        scored.Sort(static (a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : a.Cluster.CompareTo(b.Cluster);
        });

        return scored;
    }

    /// <summary>Member indices of one cluster in insertion order.</summary>
    public IReadOnlyList<int> MemberIndicesOf(int cluster) => _memberIndicesPerCluster[cluster];

    public string IdOfMember(int memberIndex) => _idOfMember[memberIndex];

    public float[] VectorOfMember(int memberIndex) => _vectorOfMember[memberIndex];
}

