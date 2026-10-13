using ProvostXMemory.Abstractions;

namespace ProvostXMemory.CorrectionTests;

/// <summary>Shared fixture builder: scored records with stable ids for grader-mock setups.</summary>
internal static class Fixtures
{
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
            Content: content ?? $"content for {id}",
            Category: category,
            Tags: tags);

        return new ScoredMemoryRecord(
            record,
            score,
            new RetrievalProvenance("test.hybrid"));
    }

    internal static RetrievalQuery Query(string text) => new(
        Text: text,
        Scope: new MemoryScope(MemoryScopeKind.Session, "s1"),
        TopK: 5);

    internal static RetrievalResult Result(params ScoredMemoryRecord[] records) => new(records);
}