using ProvostXMemory.Abstractions;

namespace ProvostXMemory.ConsolidationTests;

/// <summary>Shared record factory for consolidation tests.</summary>
public static class TestRecords
{
    public static MemoryRecord Create(
        string id,
        string key,
        string content,
        string? category = null,
        IReadOnlyList<string>? tags = null,
        DateTimeOffset? expiresAt = null,
        string scopeKind = "Session",
        string scopeId = "s1")
    {
        return new MemoryRecord(
            Id: id,
            Scope: new MemoryScope(ParseScopeKind(scopeKind), scopeId),
            Key: key,
            Content: content,
            Category: category)
        {
            Tags = tags ?? Array.Empty<string>(),
            ExpiresAt = expiresAt,
        };
    }

    // Pure parser for scope kinds; unknown values degrade to Custom.
    private static MemoryScopeKind ParseScopeKind(string value)
    {
        return Enum.TryParse<MemoryScopeKind>(value, ignoreCase: true, out var kind) ? kind : MemoryScopeKind.Custom;
    }

    public static IReadOnlyList<MemoryRecord> OrderedById(IEnumerable<MemoryRecord> records)
        => records.OrderBy(record => record.Id, StringComparer.Ordinal).ToList();
}