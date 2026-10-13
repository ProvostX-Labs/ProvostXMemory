using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Telemetry;

/// <summary>
/// Capture-style telemetry sink that records reported stages and backend stats in
/// memory. Intended for tests and lightweight in-process diagnostics; not durable.
/// </summary>
public sealed class InMemoryMemoryTelemetry : IMemoryTelemetry
{
    private readonly object _gate = new();
    private readonly List<string> _stageNames = new();
    private readonly List<double> _stageDurationsMs = new();
    private readonly List<IReadOnlyDictionary<string, double>> _stageScores = new();
    private readonly List<BackendStats> _backendStats = new();

    public Task ReportStageAsync(string stageName, double durationMs, IReadOnlyDictionary<string, double> scores)
    {
        ArgumentNullException.ThrowIfNull(stageName);
        ArgumentNullException.ThrowIfNull(scores);
        var snapshot = new Dictionary<string, double>(scores); // defensive copy: capture stays immune to caller mutation
        lock (_gate)
        {
            _stageNames.Add(stageName);
            _stageDurationsMs.Add(durationMs);
            _stageScores.Add(snapshot);
        }

        return Task.CompletedTask;
    }

    public Task ReportBackendStatsAsync(BackendStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        lock (_gate)
        {
            _backendStats.Add(stats);
        }

        return Task.CompletedTask;
    }

    /// <summary>All reported stage names, oldest first.</summary>
    public IReadOnlyList<string> StageNames
    {
        get
        {
            lock (_gate)
            {
                return new List<string>(_stageNames);
            }
        }
    }

    /// <summary>All reported stage durations in milliseconds, oldest first (parallel to <see cref="StageNames"/>).</summary>
    public IReadOnlyList<double> StageDurationsMs
    {
        get
        {
            lock (_gate)
            {
                return new List<double>(_stageDurationsMs);
            }
        }
    }

    /// <summary>All reported per-stage score dictionaries, oldest first (parallel to <see cref="StageNames"/>).</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, double>> StageScores
    {
        get
        {
            lock (_gate)
            {
                return new List<IReadOnlyDictionary<string, double>>(_stageScores);
            }
        }
    }

    /// <summary>All reported backend stats, oldest first.</summary>
    public IReadOnlyList<BackendStats> BackendStats
    {
        get
        {
            lock (_gate)
            {
                return new List<BackendStats>(_backendStats);
            }
        }
    }

    /// <summary>Drops all captured entries (test reset helper).</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _stageNames.Clear();
            _stageDurationsMs.Clear();
            _stageScores.Clear();
            _backendStats.Clear();
        }
    }
}