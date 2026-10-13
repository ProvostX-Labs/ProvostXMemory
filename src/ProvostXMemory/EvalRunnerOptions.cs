using System;
using System.Collections.Generic;

using ProvostXMemory.Eval;

namespace ProvostXMemory;

/// <summary>
/// Host-pluggable service construction for eval runs: given a fresh root path the host
/// builds an <see cref="ProvostXMemory.IMemoryService"/> exactly as it does in production.
/// Implementations must return a service with no records yet — the runner owns corpus
/// loadout and index building.
/// </summary>
public interface EvalServiceFactory
{
    /// <summary>Builds a fresh, empty memory service rooted at <paramref name="rootPath"/>.</summary>
    ProvostXMemory.IMemoryService Create(string rootPath);
}

/// <summary>Per-run knobs for <see cref="EvalRunner"/>.</summary>
public sealed class EvalRunnerOptions
{
    public const int DefaultTopK = 10;

    private static readonly EvalMetricThresholds NoThresholds = new();

    public EvalRunnerOptions(
        int topK = DefaultTopK,
        EvalMetricThresholds? thresholds = null,
        bool disposeRootDirectory = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(topK, 1);
        TopK = topK;
        Thresholds = thresholds ?? NoThresholds;
        DisposeRootDirectory = disposeRootDirectory;
    }

    /// <summary>Top-k cut for all queries and every @k metric.</summary>
    public int TopK { get; }

    /// <summary>Aggregate thresholds checked after the run.</summary>
    public EvalMetricThresholds Thresholds { get; }

    /// <summary>When true (default), the per-run temp root is deleted after the run.</summary>
    public bool DisposeRootDirectory { get; }
}