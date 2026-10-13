using System;
using System.Collections.Generic;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Retrieval;

namespace ProvostXMemory;


/// <summary>
/// Pipeline wiring knobs layered over <see cref="MemoryServiceOptions"/>: which extra
/// retrieval stages run during Recall/Remember and maintenance bounds for index updates.
/// </summary>
public sealed record MemoryServicePipelineOptions
{
    /// <summary>When true (default), Recall routes through the corrective retriever when a grader is wired.</summary>
    public bool UseCorrectiveRecall { get; init; } = true;

    /// <summary>Candidates below this score are dropped by the corrective retriever's floor.</summary>
    public double RelevanceFloor { get; init; } = 0.0;

    /// <summary>Passed through as the corrective retriever's <c>CorrectionOptions.RelevanceFloor</c>/<c>UseFetcherOnEmpty</c> values (the facade wires no fetcher itself).</summary>
    public bool UseFetcherOnEmpty { get; init; } = false;

    /// <summary>When true (default), entity/relation extraction runs during Remember (requires a wired extractor).</summary>
    public bool ExtractGraphOnRemember { get; init; } = true;

    /// <summary>
    /// When true (default), consolidation pass summaries are recorded as graph clusters linking
    /// their member entities (requires a wired extractor + graph store resolver; silently inert
    /// otherwise).
    /// </summary>
    public bool RecordClustersOnConsolidate { get; init; } = true;

    /// <summary>Upper bound of per-record representation vectors removed during index maintenance.</summary>
    public int MaxRepresentationsPerRecord { get; init; } = 8;

    /// <summary>Upper bound of per-record chunk ids probed during index maintenance.</summary>
    public int MaxChunksPerRecord { get; init; } = 64;
}

