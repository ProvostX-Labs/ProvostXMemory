using System.Collections.Generic;

namespace ProvostXMemory.Graph;

/// <summary>Entity + relation extraction result.</summary>
public sealed record ExtractionResult(
    IReadOnlyList<GraphEntity> Entities,
    IReadOnlyList<GraphRelation> Relations);