namespace ProvostXMemory.Abstractions;

/// <summary>On-disk metadata describing the memory root: codecs, counts, versions.</summary>
public sealed record MemoryManifest(
    string Version,
    long EntryCount,
    long UpdatedAtUnixMs);