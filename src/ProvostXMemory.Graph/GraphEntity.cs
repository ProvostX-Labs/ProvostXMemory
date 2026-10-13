using System.Collections.Generic;

namespace ProvostXMemory.Graph;

/// <summary>
/// An extracted graph entity. <see cref="EntityId"/> is the stable slug id;
/// <see cref="Name"/> is the canonical display form; <see cref="Aliases"/> holds
/// alternative spellings/casings; <see cref="RecordIds"/> lists the records mentioning it.
/// </summary>
public sealed record GraphEntity(
    string EntityId,
    string Name,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> RecordIds);