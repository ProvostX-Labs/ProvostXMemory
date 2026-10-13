namespace ProvostXMemory.Abstractions;

/// <summary>
/// Chunking parameters: structural markers, size and overlap. A trailing chunk shorter
/// than <see cref="MinChunkLength"/> merges into its predecessor (may exceed the max —
/// max is a packing target, min is a hard floor).
/// </summary>
public sealed record ChunkOptions(
    int MaxChunkLength = 512,
    int Overlap = 0,
    bool RespectStructure = true,
    string? Separator = null,
    int MinChunkLength = 0);