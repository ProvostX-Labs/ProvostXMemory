using System;
using System.IO;

namespace ProvostXMemory.Storage;

/// <summary>
/// Root folder + scope path resolution: <c>&lt;root&gt;/&lt;scope-kind&gt;/&lt;scope-id&gt;/...</c>.
/// Every caller-supplied segment is validated to be a single, safe path segment
/// (no traversal, no separators, no Windows-reserved names) and every resolved
/// path is checked to stay inside the root folder.
/// </summary>
public sealed class MemoryRootLayout
{
    private const int MaxSegmentLength = 128; // compile-time constant; no shared mutable state

    private readonly string _rootPath;
    private readonly string _defaultModelHash;

    public MemoryRootLayout(string rootPath, string defaultModelHash)
    {
        ArgumentNullException.ThrowIfNull(rootPath);
        ArgumentNullException.ThrowIfNull(defaultModelHash);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Root path is required.", nameof(rootPath));
        }

        if (!IsSafeSegment(defaultModelHash))
        {
            throw new ArgumentException("Default model hash is not a safe path segment.", nameof(defaultModelHash));
        }

        _rootPath = rootPath;
        _defaultModelHash = defaultModelHash;
    }

    public string RootPath => _rootPath;

    public string DefaultModelHash => _defaultModelHash;

    public string ManifestPath => CombineValidated("manifest.json");

    public string ScopePath(string scopeKind, string scopeId)
        => CombineValidated(RequireSegment(scopeKind, "scopeKind"), RequireSegment(scopeId, "scopeId"));

    public string ScopePath(string scopeKind, string scopeId, string modelHash)
        => CombineValidated(
            RequireSegment(scopeKind, "scopeKind"),
            RequireSegment(scopeId, "scopeId"),
            RequireSegment(modelHash, "modelHash"));

    public string RecordsPath(string scopeKind, string scopeId)
        => CombineValidated(
            RequireSegment(scopeKind, "scopeKind"),
            RequireSegment(scopeId, "scopeId"),
            "records.jsonl");

    public string ChunksPath(string scopeKind, string scopeId)
        => CombineValidated(
            RequireSegment(scopeKind, "scopeKind"),
            RequireSegment(scopeId, "scopeId"),
            "chunks");

    public string VectorsPath(string scopeKind, string scopeId, string modelHash)
        => CombineValidated(
            RequireSegment(scopeKind, "scopeKind"),
            RequireSegment(scopeId, "scopeId"),
            "vectors",
            RequireSegment(modelHash, "modelHash"));

    public string GraphPath(string scopeKind, string scopeId, string fileName)
        => CombineValidated(
            RequireSegment(scopeKind, "scopeKind"),
            RequireSegment(scopeId, "scopeId"),
            "graph",
            RequireSegment(fileName, "fileName"));

    private string CombineValidated(params string[] segments)
    {
        var parts = new string[segments.Length + 1];
        parts[0] = _rootPath;
        for (var i = 0; i < segments.Length; i++)
        {
            parts[i + 1] = segments[i];
        }

        var full = Path.GetFullPath(Path.Combine(parts));
        var rootFull = Path.GetFullPath(_rootPath);
        if (!rootFull.EndsWith(Path.DirectorySeparatorChar) && !rootFull.EndsWith(Path.AltDirectorySeparatorChar))
        {
            rootFull += Path.DirectorySeparatorChar;
        }

        if (!full.StartsWith(rootFull, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Resolved memory path escapes the root folder.");
        }

        return full;
    }

    // Pure function: legality of a single path segment; no shared mutable state.
    private static bool IsSafeSegment(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxSegmentLength)
        {
            return false;
        }

        if (!char.IsAsciiLetterOrDigit(value[0]))
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            var c = value[i];
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-' or '~' or '.'))
            {
                return false;
            }
        }

        if (value[^1] == '.')
        {
            return false;
        }

        return !IsWindowsReservedName(value);
    }

    // Pure function: Windows-reserved device names are never safe segments.
    private static bool IsWindowsReservedName(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "con" or "prn" or "aux" or "nul"
                or "com1" or "com2" or "com3" or "com4" or "com5" or "com6" or "com7" or "com8" or "com9"
                or "lpt1" or "lpt2" or "lpt3" or "lpt4" or "lpt5" or "lpt6" or "lpt7" or "lpt8" or "lpt9" => true,
            _ => false,
        };
    }

    private static string RequireSegment(string value, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsSafeSegment(value))
        {
            throw new ArgumentException("Value is not a safe single path segment.", paramName);
        }

        return value;
    }
}