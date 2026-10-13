using System.Globalization;
using System.Text.RegularExpressions;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Logging;

/// <summary>Deletes log files older than the configured retention window.</summary>
public sealed class RetentionSweeper
{
    private static readonly string DatePlaceholder = "{date}";
    private readonly MemoryLoggingOptions _options;
    private readonly Func<DateTimeOffset> _utcNow;

    /// <summary>Creates a retention sweeper.</summary>
    /// <param name="options">Logging options providing directory, pattern and retention window.</param>
    /// <param name="utcNow">Optional UTC-now override (used by tests).</param>
    public RetentionSweeper(MemoryLoggingOptions options, Func<DateTimeOffset>? utcNow = null)
    {
        _options = options;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    // Stateless utility (pure): string/date parsing only, no state.
    /// <summary>Extracts the date from a file name produced by the pattern (pure).</summary>
    public static DateOnly? TryParseFileDate(string fileName, string fileNamePattern)
    {
        var regex = BuildPatternRegex(fileNamePattern);
        var match = regex.Match(fileName);
        if (!match.Success)
        {
            return null;
        }

        return DateOnly.TryParseExact(
            match.Groups["date"].Value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : null;
    }

    // Stateless utility (pure): compiles a fresh regex from the pattern; no state.
    private static Regex BuildPatternRegex(string fileNamePattern)
    {
        var escaped = Regex.Escape(fileNamePattern);
        var pattern = escaped.Replace(Regex.Escape(DatePlaceholder), @"(?<date>\d{4}-\d{2}-\d{2})");
        return new Regex("^" + pattern + "$", RegexOptions.ExplicitCapture);
    }

    /// <summary>Sweeps the log directory, deleting files older than RetentionDays.</summary>
    /// <returns>The number of files deleted.</returns>
    public int Sweep()
    {
        if (_options.RetentionDays is not { } retentionDays)
        {
            return 0;
        }

        var directory = _options.Directory;
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var cutoff = _utcNow().UtcDateTime.Date.AddDays(-retentionDays);
        var deleted = 0;
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var date = TryParseFileDate(Path.GetFileName(file), _options.FileNamePattern);
            if (date is not null && date.Value < DateOnly.FromDateTime(cutoff))
            {
                File.Delete(file);
                deleted++;
            }
        }

        return deleted;
    }
}
