using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Graph;

/// <summary>Host-injected entity/relation extractor (LLM or heuristic).</summary>
public interface IEntityExtractor
{
    Task<ExtractionResult> ExtractAsync(IReadOnlyList<MemoryRecord> records, CancellationToken cancellationToken = default);
}