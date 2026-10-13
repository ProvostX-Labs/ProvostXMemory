using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProvostXMemory.Abstractions;


/// <summary>
/// The isolation axis of a <see cref="MemoryScope"/>. One record lives in exactly one
/// (Kind, Id) pair; the kind determines what the id conventionally names:
/// <c>Platform</c> (one surface — e.g. per chat platform), <c>Session</c>
/// (one conversation or run), <c>Tenant</c> (one customer — multi-tenant hosts),
/// <c>Purpose</c> (one knowledge base or domain), <c>Custom</c> (host-defined).
/// Kinds map to storage path segments, so cross-kind and cross-id isolation are identical
/// by construction. Unknown on-disk kinds degrade to <c>Custom</c> on read.
/// </summary>
public enum MemoryScopeKind
{
    Platform,
    Session,
    Tenant,
    Purpose,
    Custom,
}

