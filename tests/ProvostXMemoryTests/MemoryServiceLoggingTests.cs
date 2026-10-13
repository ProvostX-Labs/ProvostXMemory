using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Consolidation;
using ProvostXMemory.Fusion;
using ProvostXMemory.Graph;
using ProvostXMemory.Logging;
using ProvostXMemory.Storage;
using ProvostXMemory.Telemetry;
using ProvostXMemory.VectorIndex.Engine;

using Xunit;

namespace ProvostXMemoryTests;

/// <summary>
/// Facade logging wiring: disabled logging creates no log file and costs nothing;
/// enabled logging with the injected sink emits Lifecycle/IndexBuild/Consolidation/
/// ScopeRejection/StoreError events; record content never appears in log output by
/// default (IncludeContent=false).
/// </summary>
public sealed class MemoryServiceLoggingTests : IDisposable
{
    private const string RootDir = "pvmx-facade-logging-tests";

    private readonly string _rootPath;

    public MemoryServiceLoggingTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), RootDir, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_rootPath, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private MemoryServiceOptions Options(MemoryLoggingOptions? logging = null) => new(
        new StorageOptions(_rootPath),
        new VectorOptions("model-hash-v1", 128),
        GraphOptions.Default,
        new FusionOptions(),
        new ConsolidationOptions(),
        Logging: logging);

    private MemoryService Create(MemoryServiceOptions options, IMemoryLogSink? sink = null)
    {
        var builder = new MemoryServiceBuilder()
            .WithOptions(options)
            .WithStore(new FacadeStore())
            .WithEmbedder(new HashEmbedder())
            .WithIndexFactory(new EngineVectorIndexFactory())
            .WithFuser(new ReciprocalRankFuser())
            .WithTelemetry(NoopMemoryTelemetry.Instance);
        if (sink is not null)
        {
            builder.WithLogSink(sink);
        }

        return builder.Build();
    }

    private static MemoryScope Scope() => new(MemoryScopeKind.Session, "logging");

    [Fact]
    public async Task Disabled_logging_creates_no_log_file()
    {
        var logsDir = Path.Combine(_rootPath, "logs");
        var service = Create(Options(new MemoryLoggingOptions(Enabled: false, Directory: logsDir)));
        var scope = Scope();

        await service.RememberAsync(new MemoryRecord("r1", scope, "key", "secret-content-alpha"));
        await service.RecallAsync(new RetrievalQuery("secret", scope));
        await service.BuildIndexesAsync(scope);

        Assert.False(Directory.Exists(logsDir));
    }

    [Fact]
    public async Task Enabled_logging_emits_lifecycle_indexbuild_and_consolidation_events()
    {
        var sink = new FacadeRecordingLogSink();
        var service = Create(
            Options(new MemoryLoggingOptions(Enabled: true, Sink: MemoryLogSinkKind.Injected)),
            sink);
        var scope = Scope();

        await service.RememberAsync(new MemoryRecord("r1", scope, "key", "alpha"));
        await service.BuildIndexesAsync(scope);
        await service.ConsolidateAsync(scope, new ConsolidationOptions());

        var categories = sink.Entries.Select(entry => entry.Category).ToArray();
        Assert.Contains(MemoryLogCategory.Lifecycle, categories); // service built
        Assert.Equal(2, categories.Count(category => category == MemoryLogCategory.Lifecycle)); // + scope indexes opened
        Assert.Contains(MemoryLogCategory.IndexBuild, categories);
        Assert.Contains(MemoryLogCategory.Consolidation, categories);
        Assert.Contains(sink.Entries, entry => entry.Category == MemoryLogCategory.Lifecycle && entry.Scope is not null);
    }

    [Fact]
    public async Task Scope_rejection_and_store_error_events_are_emitted()
    {
        var sink = new FacadeRecordingLogSink();
        var service = Create(
            Options(new MemoryLoggingOptions(Enabled: true, Sink: MemoryLogSinkKind.Injected)),
            sink);
        var scope = Scope();

        // Update of a missing record → KeyNotFoundException from the store → ScopeRejection warn.
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.UpdateAsync(new MemoryRecord("missing", scope, "key", "text")));

        // Remember of a duplicate id → InvalidOperationException from the store → StoreError error.
        await service.RememberAsync(new MemoryRecord("dup", scope, "key", "one"));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RememberAsync(new MemoryRecord("dup", scope, "key", "two")));

        Assert.Contains(
            sink.Entries,
            entry => entry.Category == MemoryLogCategory.ScopeRejection && entry.Level == MemoryLogLevel.Warn);
        Assert.Contains(
            sink.Entries,
            entry => entry.Category == MemoryLogCategory.StoreError && entry.Level == MemoryLogLevel.Error);
    }

    [Fact]
    public async Task Content_is_never_present_in_log_output_by_default()
    {
        var logsDir = Path.Combine(_rootPath, "logs");
        var service = Create(Options(new MemoryLoggingOptions(
            Enabled: true,
            Sink: MemoryLogSinkKind.File,
            Directory: logsDir)));
        const string secret = "sourdough-starter-ferment-secret-xyzzy";
        var scope = Scope();

        await service.RememberAsync(new MemoryRecord("r1", scope, "key", secret));
        await service.BuildIndexesAsync(scope);
        await service.ConsolidateAsync(scope, new ConsolidationOptions());
        await service.ForgetAsync(scope, "missing-record-id");

        // Give the fire-and-forget writes a moment to land.
        await Task.Delay(200);

        var logFiles = Directory.Exists(logsDir) ? Directory.GetFiles(logsDir) : Array.Empty<string>();
        Assert.NotEmpty(logFiles);
        var text = string.Join(Environment.NewLine, logFiles.Select(File.ReadAllText));
        Assert.DoesNotContain(secret, text);
        Assert.DoesNotContain("key", text, StringComparison.Ordinal);
    }
}
