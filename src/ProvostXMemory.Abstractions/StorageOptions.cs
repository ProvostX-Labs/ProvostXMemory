namespace ProvostXMemory.Abstractions;

/// <summary>FlatJson store options: root folder + durability settings.</summary>
public sealed record StorageOptions(
    string RootPath,
    bool AtomicWrites = true,
    bool Fsync = false);
