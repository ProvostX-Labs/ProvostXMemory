using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Logging;

/// <summary>
/// Facade over the logging pipeline: on/off gate, level filter, hash chaining
/// and fan-out to the configured sink. When disabled, no entry objects are
/// allocated and no writes occur.
/// </summary>
public sealed class MemoryLogger
{
    private readonly MemoryLoggingOptions _options;
    private readonly IMemoryLogSink? _sink;
    private readonly HashChainAppender? _chain;
    private readonly MemoryLogLevel _minimumLevel;
    private readonly object _chainGate = new();

    /// <summary>Creates a logger from options and an optional injected sink.</summary>
    /// <param name="options">Logging options.</param>
    /// <param name="injectedSink">Required when <paramref name="options"/> selects the Injected sink.</param>
    /// <param name="injectedChain">Optional pre-seeded hash chain appender.</param>
    public MemoryLogger(MemoryLoggingOptions options, IMemoryLogSink? injectedSink = null, HashChainAppender? injectedChain = null)
    {
        _options = options;
        _minimumLevel = options.Level;
        if (!options.Enabled || options.Sink == MemoryLogSinkKind.None)
        {
            _sink = null;
            return;
        }

        _sink = options.Sink switch
        {
            MemoryLogSinkKind.File => new FileMemoryLogSink(options.Directory, options.FileNamePattern, options.Format),
            MemoryLogSinkKind.Console => new ConsoleMemoryLogSink(),
            MemoryLogSinkKind.Injected => injectedSink ?? throw new ArgumentException("The Injected sink requires a sink instance.", nameof(injectedSink)),
            _ => null,
        };
        if (_sink is not null && options.Integrity == MemoryLogIntegrity.HashChain)
        {
            _chain = injectedChain ?? new HashChainAppender();
        }
    }

    /// <summary>Gets a value indicating whether logging is active. Check before allocating entry data.</summary>
    public bool IsEnabled => _sink is not null;

    /// <summary>Writes an entry if logging is enabled and the level passes the filter.</summary>
    /// <param name="level">Severity.</param>
    /// <param name="category">Functional area.</param>
    /// <param name="message">Message (must not embed memory content unless IncludeContent is enabled).</param>
    /// <param name="scope">Optional scope.</param>
    /// <param name="correlationId">Optional correlation id.</param>
    /// <param name="durationMs">Optional duration.</param>
    /// <param name="content">Raw content; only captured when IncludeContent is true.</param>
    public void Log(
        MemoryLogLevel level,
        MemoryLogCategory category,
        string message,
        MemoryLogScope? scope = null,
        string? correlationId = null,
        double? durationMs = null,
        string? content = null)
    {
        if (_sink is null || level < _minimumLevel)
        {
            return;
        }

        var entry = new MemoryLogEntry(
            TimestampUtc: DateTimeOffset.UtcNow,
            SequenceNumber: 0L,
            Level: level,
            Category: category,
            Message: message,
            Scope: scope,
            CorrelationId: correlationId,
            DurationMs: durationMs,
            ContentExcerpt: _options.IncludeContent && content is not null ? content : null);
        if (_chain is not null)
        {
            // Hash-chain mode: sequence assignment and the file write must be serialized and
            // ordered — a concurrent fire-and-forget write could interleave chain links and
            // duplicate sequence numbers, corrupting the audit chain. Log synchronously under
            // a gate instead. Logging failures are still swallowed by WriteAsync's handler.
            lock (_chainGate)
            {
                entry = _chain.Append(entry);
                WriteAsync(entry).GetAwaiter().GetResult();
            }

            return;
        }

        _ = WriteAsync(entry);
    }

    private async Task WriteAsync(MemoryLogEntry entry)
    {
        if (_sink is null)
        {
            return;
        }

        try
        {
            await _sink.WriteAsync(entry).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Logging must never take the host operation down.
        }
    }
}
