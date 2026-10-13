using System;
using System.Globalization;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Chunking;


/// <summary>The four components of a canonical chunk id.</summary>
public sealed record ChunkIdParts(
    MemoryScopeKind ScopeKind,
    string ScopeId,
    string RecordId,
    int ChunkIndex);
