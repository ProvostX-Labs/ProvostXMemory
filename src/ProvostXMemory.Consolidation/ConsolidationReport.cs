namespace ProvostXMemory.Consolidation;

/// <summary>Outcome counters of one consolidation pass over a scope.</summary>
public sealed record ConsolidationReport
{
    /// <summary>Records visible when the pass started.</summary>
    public int RecordsConsidered { get; init; }

    /// <summary>Records removed because their TTL had elapsed.</summary>
    public int ExpiredDeleted { get; init; }

    /// <summary>Duplicate records removed by dedup (every member of a duplicate group except the oldest kept).</summary>
    public int DuplicatesDeleted { get; init; }

    /// <summary>Summary records appended for near-duplicate clusters.</summary>
    public int SummariesEmitted { get; init; }

    /// <summary>Records remaining in the scope after the pass.</summary>
    public int RecordsRemaining { get; init; }
}