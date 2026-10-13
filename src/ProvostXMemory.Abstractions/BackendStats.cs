using System;
using System.Collections.Generic;

namespace ProvostXMemory.Abstractions;

/// <summary>Stats snapshot of a backend (store or index).</summary>
public sealed record BackendStats(
    long Count,
    long BytesOnDisk,
    DateTimeOffset? LastFlushedAt = null);