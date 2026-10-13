namespace ProvostXMemory.Abstractions;

/// <summary>A scored hit from a store/index search: scope kind, scope id, id, score.</summary>
public sealed record ScoredId(
    string ScopeKind,
    string ScopeId,
    string Id,
    double Score);