namespace ProvostXMemory.Logging;

/// <summary>Functional area a memory log entry belongs to.</summary>
public enum MemoryLogCategory
{
    /// <summary>Retrieval operations.</summary>
    Retrieval,
    /// <summary>Index building.</summary>
    IndexBuild,
    /// <summary>Memory consolidation.</summary>
    Consolidation,
    /// <summary>Scope rejection events.</summary>
    ScopeRejection,
    /// <summary>Store-level errors.</summary>
    StoreError,
    /// <summary>Service lifecycle events.</summary>
    Lifecycle,
}
