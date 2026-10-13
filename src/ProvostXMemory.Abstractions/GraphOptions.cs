namespace ProvostXMemory.Abstractions;

/// <summary>Graph layer options: neighbor fan-out cap, walk depth, retriever behavior knobs.</summary>
public sealed record GraphOptions(
    bool Enabled = true,
    int MaxNeighbors = 16,
    int MaxHops = 2,
    int MaxClusters = 5)
{
    public static GraphOptions Default { get; } = new(); // pure constant factory; no shared mutable state
}