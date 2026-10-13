using System.Collections.Generic;

namespace ProvostXMemory.Graph;

/// <summary>
/// A relation between two graph entities, treated as undirected for traversal.
/// Weight is implicit: <see cref="RecordIds"/>.Count equals the number of supporting records.
/// </summary>
public sealed record GraphRelation(
    string RelationId,
    string FromEntityId,
    string ToEntityId,
    string Label,
    IReadOnlyList<string> RecordIds);