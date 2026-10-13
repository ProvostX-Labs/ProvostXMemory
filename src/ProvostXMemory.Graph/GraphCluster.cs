using System.Collections.Generic;

namespace ProvostXMemory.Graph;

/// <summary>A graph cluster (community) with an optional summary record id.</summary>
public sealed record GraphCluster(
    string ClusterId,
    IReadOnlyList<string> EntityIds,
    string? SummaryRecordId);