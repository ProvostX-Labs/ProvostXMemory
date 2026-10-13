
namespace ProvostXMemory.Abstractions;

/// <summary>
/// <para>The root options record for <c>MemoryService</c>: the host's per-domain
/// tuning, injected once at build time. Each section covers one pipeline concern —
/// <see cref="Storage"/> (root layout), <see cref="Vector"/> (backend + dim),
/// <see cref="Graph"/> (walk tuning), <see cref="Fusion"/> (RRF weights),
/// <see cref="Consolidation"/> (dedup/cluster thresholds), <see cref="Logging"/></para>
/// <para>Section records carry safe defaults; a host that passes all defaults gets the
/// reference pipeline. Optional capabilities (grader, graph extractor, reranker, log
/// sink) are injected via <c>MemoryServiceBuilder</c> rather than this record —
/// options tune behavior, wiring enables capabilities.</para>
/// </summary>
public sealed record MemoryServiceOptions(
    StorageOptions Storage,
    VectorOptions Vector,
    GraphOptions Graph,
    FusionOptions Fusion,
    ConsolidationOptions Consolidation,
    MemoryLoggingOptions? Logging = null);
