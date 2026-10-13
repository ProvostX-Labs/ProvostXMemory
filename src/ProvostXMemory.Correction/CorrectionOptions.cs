namespace ProvostXMemory.Correction;

/// <summary>
/// Options for the corrective retrieval pipeline. Typed options injected by the host
/// (no environment configuration in product code).
/// </summary>
/// <param name="RelevanceFloor">Relevant candidates whose score is below this floor are dropped before yielding.</param>
/// <param name="UseFetcherOnEmpty">
/// When false the external fetch stage is skipped entirely; otherwise the injected fetcher
/// (if any) runs when nothing survived the corrective loop.
/// </param>
public sealed record CorrectionOptions(
    double RelevanceFloor = 0.0,
    bool UseFetcherOnEmpty = true);