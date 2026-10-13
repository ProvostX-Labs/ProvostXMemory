using System.Globalization;
using System.Text;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Logging;

/// <summary>
/// Append-only file sink with per-date rolling and lazy directory creation.
/// Each line is written with <c>FileShare.Read</c> and flushed immediately.
/// </summary>
public sealed class FileMemoryLogSink : IMemoryLogSink
{
    private static readonly string DatePlaceholder = "{date}";
    private readonly string _directory;
    private readonly string _fileNamePattern;
    private readonly MemoryLogFormat _format;
    private readonly SemaphoreSlim _gate = new(1, 1);
    /// <summary>Creates a file sink.</summary>
    /// <param name="directory">Log directory (created lazily on first write).</param>
    /// <param name="fileNamePattern">Pattern containing <c>{date}</c>.</param>
    /// <param name="format">Line format.</param>
    public FileMemoryLogSink(string directory, string fileNamePattern, MemoryLogFormat format)
    {
        _directory = directory;
        _fileNamePattern = fileNamePattern;
        _format = format;
    }

    // Stateless utility (pure): deterministic path composition, no I/O or state.
    /// <summary>Resolves the file path for a given UTC date (pure).</summary>
    public static string ResolvePath(string directory, string fileNamePattern, DateOnly date)
    {
        var stamp = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var name = fileNamePattern.Contains(DatePlaceholder, StringComparison.Ordinal)
            ? fileNamePattern.Replace(DatePlaceholder, stamp, StringComparison.Ordinal)
            : fileNamePattern;
        return Path.Combine(directory, name);
    }

    // Stateless utility (pure): formats a line from the entry alone, no state.
    /// <summary>Formats an entry as a single line (pure).</summary>
    public static string FormatLine(MemoryLogEntry entry, MemoryLogFormat format)
    {
        if (format == MemoryLogFormat.JsonLines)
        {
            return MemoryLogJson.ToLine(entry);
        }

        var builder = new StringBuilder();
        builder.Append(entry.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
        builder.Append(" [").Append(entry.Level).Append("] ");
        builder.Append(entry.Category).Append(" : ").Append(entry.Message);
        if (entry.Scope is not null)
        {
            builder.Append(" scope=").Append(entry.Scope.Kind).Append(':').Append(entry.Scope.Id);
        }

        if (entry.CorrelationId is not null)
        {
            builder.Append(" corr=").Append(entry.CorrelationId);
        }

        if (entry.DurationMs is not null)
        {
            builder.Append(" durMs=").Append(entry.DurationMs.Value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        if (entry.PreviousHash is not null)
        {
            builder.Append(" prevHash=").Append(entry.PreviousHash);
        }

        if (entry.EntryHash is not null)
        {
            builder.Append(" hash=").Append(entry.EntryHash);
        }

        if (entry.ContentExcerpt is not null)
        {
            builder.Append(" excerpt=").Append(entry.ContentExcerpt);
        }

        return builder.ToString();
    }

    /// <inheritdoc />
    public async Task WriteAsync(MemoryLogEntry entry, CancellationToken cancellationToken = default)
    {
        var line = FormatLine(entry, _format);
        var path = ResolvePath(
            _directory,
            _fileNamePattern,
            DateOnly.FromDateTime(entry.TimestampUtc.UtcDateTime));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_directory);
            await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
