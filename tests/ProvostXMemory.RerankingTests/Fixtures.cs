using ProvostXMemory.Abstractions;

namespace ProvostXMemory.RerankingTests;

/// <summary>Shared fixture builder: scored records with controllable keys/tags/content lengths.</summary>
internal static class Fixtures
{
    // Neutral content exactly at MinLength (8) → no length penalty by default.
    internal const string NeutralContent = "12345678";

    internal static ScoredMemoryRecord Rec(
        string id,
        double score,
        string? key = null,
        string? content = null,
        string? category = null,
        string[]? tags = null)
    {
        var record = new MemoryRecord(
            Id: id,
            Scope: new MemoryScope(MemoryScopeKind.Session, "s1"),
            Key: key ?? id,
            Content: content ?? NeutralContent,
            Category: category,
            Tags: tags);

        return new ScoredMemoryRecord(
            record,
            score,
            new RetrievalProvenance("test.fusion"));
    }

    internal static RetrievalQuery Query(string text) => new(
        Text: text,
        Scope: new MemoryScope(MemoryScopeKind.Session, "s1"),
        TopK: 5);
}