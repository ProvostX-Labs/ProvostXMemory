using System;
using System.IO;

namespace ProvostXMemory.Storage;

/// <summary>
/// Durability primitive for the storage layer: writes payload to a temporary
/// file in the destination directory, fsyncs it, then atomically moves it over
/// the destination. Temp leftovers from crashed writers are cleaned up on the
/// next write to the same directory.
/// </summary>
public sealed class AtomicFileWriter
{
    // Pure-function prefix factory: builds collision-free temp names from tick + guid; no shared mutable state.
    private static readonly Func<String> TempPrefixFactory = static () => DateTimeOffset.UtcNow.UtcTicks.ToString("x16");

    private readonly bool _fsync;

    public AtomicFileWriter(bool fsync = false)
    {
        _fsync = fsync;
    }

    /// <summary>Result of an atomic write: the destination path and the bytes written.</summary>
    public sealed record WriteResult(string DestinationPath, long BytesWritten)
    {
    }

    /// <summary>Writes <paramref name="payload"/> to <paramref name="destinationPath"/> transactionally.</summary>
    public WriteResult Write(string destinationPath, ReadOnlySpan<char> payload)
    {
        ArgumentNullException.ThrowIfNull(destinationPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException("Destination path has no directory part.", nameof(destinationPath));
        }

        Directory.CreateDirectory(directory);
        var tempPath = NextTempPath(directory, Path.GetFileName(destinationPath));

        try
        {
            WriteTempFile(tempPath, payload);
            File.Move(tempPath, destinationPath, overwrite: true);
            return new WriteResult(destinationPath, new FileInfo(destinationPath).Length);
        }
        finally
        {
            CleanupTemp(tempPath, destinationPath);
        }
    }

    private void WriteTempFile(string tempPath, ReadOnlySpan<char> payload)
    {
        using (var stream = new FileStream(
                   tempPath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None,
                   bufferSize: 1 << 16,
                   FileOptions.SequentialScan))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(payload);
            writer.Flush();

            if (_fsync)
            {
                stream.Flush(flushToDisk: true);
            }
        }
    }

    /// <summary>Removes stale temp files next to <paramref name="path"/> (crash debris from interrupted writers).</summary>
    public void CleanupStaleTempFiles(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return;
        }

        var target = Path.GetFileName(path);
        foreach (var temp in Directory.GetFiles(directory, target + ".*.tmp"))
        {
            TryDelete(temp);
        }
    }

    // Pure function: temp file name for one destination + unique token; no shared mutable state.
    private static string NextTempPath(string directory, string fileName)
    {
        var token = $"{TempPrefixFactory()}-{Guid.NewGuid():N}";
        return Path.Combine(directory, fileName + "." + token + ".tmp");
    }

    private static void TryDelete(string candidate)
    {
        try
        {
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
        catch (IOException)
        {
            // Best effort only.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort only.
        }
    }

    private static void CleanupTemp(string tempPath, string destinationPath)
    {
        if (!string.Equals(tempPath, destinationPath, StringComparison.Ordinal) && File.Exists(tempPath))
        {
            TryDelete(tempPath);
        }
    }
}