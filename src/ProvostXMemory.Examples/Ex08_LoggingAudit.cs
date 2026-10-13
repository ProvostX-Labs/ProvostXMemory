using ProvostXMemory.Abstractions;
using ProvostXMemory.Logging;

namespace ProvostXMemory.Examples;

/// <summary>
/// Audit logging with hash-chain integrity: file logging is enabled, service operations
/// append chained entries, <see cref="LogChainVerifier"/> validates the chain — and after
/// one line is tampered with, the verifier pinpoints the broken line.
/// </summary>
public sealed class Ex08_LoggingAudit : ExampleBase
{
    public override async Task RunAsync()
    {
        var rig = new ExampleRig();
        var root = rig.FreshRoot("ex08");
        var logDirectory = Path.Combine(root, "logs");

        IMemoryService memory = rig.BuildService(root, logging: new MemoryLoggingOptions(
            Enabled: true,
            Level: MemoryLogLevel.Info,
            Sink: MemoryLogSinkKind.File,
            Directory: logDirectory,
            Format: MemoryLogFormat.JsonLines,
            IncludeContent: false, // content NEVER logged by default
            Integrity: MemoryLogIntegrity.HashChain));
        var scope = new MemoryScope(MemoryScopeKind.Session, "audit-demo");

        // Ordinary operations: each writes hash-chained log entries (lifecycle, index build…).
        await memory.RememberAsync(new MemoryRecord(
            "audit-1", scope, "rollout-note",
            "kubernetes rollout completed without downtime")).ConfigureAwait(false);
        await memory.BuildIndexesAsync(scope).ConfigureAwait(false);
        await memory.RecallAsync(new RetrievalQuery("kubernetes rollout", scope, TopK: 3))
            .ConfigureAwait(false);

        var logFiles = Directory.GetFiles(logDirectory, "memory-*.log");
        if (logFiles.Length == 0)
        {
            throw new InvalidOperationException("Expected at least one log file to be written.");
        }

        var logFile = logFiles[0];
        var lines = File.ReadAllLines(logFile);
        Console.WriteLine($"Log file: {Path.GetFileName(logFile)} ({lines.Length} hash-chained entries)");

        var clean = LogChainVerifier.Verify(logFile);
        Console.WriteLine($"Verify (clean):  valid={clean.IsValid}" + (clean.IsValid ? "" : $" line={clean.FirstBrokenLine}: {clean.Reason}"));

        // Tamper: rewrite one entry's message in place.
        var tamperedIndex = Array.FindIndex(lines, line => line.Contains("memory service built", StringComparison.Ordinal));
        if (tamperedIndex < 0)
        {
            tamperedIndex = 0;
        }

        lines[tamperedIndex] = lines[tamperedIndex].Replace("memory service built", "memory service ATTACKED", StringComparison.Ordinal);
        var tamperedFile = Path.Combine(logDirectory, "memory-tampered-simulation.log");
        File.WriteAllLines(tamperedFile, lines);

        var broken = LogChainVerifier.Verify(tamperedFile);
        Console.WriteLine($"Verify (tampered): valid={broken.IsValid} at line {broken.FirstBrokenLine}: {broken.Reason}");
        Console.WriteLine();
        Console.WriteLine("Hash-chain integrity turns silent tampering into a loud, line-precise failure.");
    }
}
