namespace ProvostXMemory.Logging;

/// <summary>Scope information attached to a log entry.</summary>
/// <param name="Kind">Kind of scope (e.g. "session", "agent").</param>
/// <param name="Id">Scope identifier.</param>
public sealed record MemoryLogScope(string Kind, string Id);
