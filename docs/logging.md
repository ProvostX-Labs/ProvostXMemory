# Logging and Audit Hash-Chain

ProvostXMemory ships a self-contained logging facility with an audit-grade integrity mode: append-only files, a SHA-256 hash chain for tamper evidence, rotation, retention, and strict content minimization.

## Disabled by default

Logging is off by default with **zero cost**: no files are created, no allocations occur. Enable via `MemoryServiceOptions.Logging`:

```csharp
Logging: new MemoryLoggingOptions(
    Enabled: true,
    Level: MemoryLogLevel.Info,
    Sink: MemoryLogSinkKind.File,          // File | Console | Injected
    Directory: Path.Combine(root, "logs"),
    FileNamePattern: "memory-{date}.log",
    Format: MemoryLogFormat.JsonLines,
    IncludeContent: false,                 // content NEVER logged by default
    Integrity: MemoryLogIntegrity.HashChain,
    RetentionDays: 30)
```

## Options reference

| Option | Type / default | Description |
|---|---|---|
| `Enabled` | `bool`, `false` | Master switch. Disabled ⇒ no writes, no allocations. |
| `Level` | `MemoryLogLevel`, `Info` | Minimum level emitted. |
| `Sink` | `MemoryLogSinkKind`, `File` | `File` (rolling files), `Console`, or `Injected` (host-provided `IMemoryLogSink` via `MemoryServiceBuilder.WithLogSink`). |
| `Directory` | `string` | Log directory (typically under the memory root). |
| `FileNamePattern` | `string` | Rolling file name pattern. |
| `Format` | `MemoryLogFormat`, `JsonLines` | Line format; `MemoryLogJson` provides the encoder. |
| `IncludeContent` | `bool`, `false` | When false (default), record content never appears in any output — at most a short excerpt when explicitly enabled. Pinned by content-leak tests. |
| `Integrity` | `MemoryLogIntegrity`, `HashChain` | Enables the SHA-256 audit hash chain. |
| `RetentionDays` | `int?` | Retention window enforced by `RetentionSweeper`. |

## Emitted categories

| Category | Level | Meaning |
|---|---|---|
| `Lifecycle` | info | Service built; scope indexes opened. |
| `IndexBuild` | info | Vector/lexical index builds per scope. |
| `Consolidation` | info | Consolidation pass activity. |
| `ScopeRejection` | warn | Store rejected an operation for a missing scope or record id. |
| `StoreError` | error | Storage-layer failures. |

Each `MemoryLogEntry` carries a timestamp, monotonically increasing sequence number, level, category, optional `MemoryLogScope` (kind + id), correlation id, duration, and the chain fields.

## The audit hash chain

When `Integrity = MemoryLogIntegrity.HashChain`, every entry is chained:

```
entryHash(n) = SHA-256( sequence(n) ‖ timestamp(n) ‖ level ‖ category ‖ payload ‖ previousHash )
```

`HashChainAppender` computes and appends the chain; `previousHash` of entry *n* is `entryHash(n-1)`, so any modification, deletion, or reordering of historical entries breaks every subsequent link.

### Verifying a log

`LogChainVerifier` walks a log file (or stream) from the beginning and returns a `LogChainVerificationResult`:

```csharp
var verifier = new LogChainVerifier();
LogChainVerificationResult result = verifier.Verify(logFilePath);

if (!result.IsValid)
{
    // result.BrokenAtSequence: first sequence number where the chain broke
    // result.Detail: human-readable failure description
}
```

A valid result means the log is complete and unmodified from the first entry onward. This makes the log admissible as internal audit evidence: silent edits are detectable, and truncation is detectable as a chain break at the missing sequence.

## Retention and rotation

- `RetentionSweeper` deletes log files older than `RetentionDays`. Run it on your schedule (e.g. at service start) — retention is explicit, never implicit.
- Rotation follows `FileNamePattern`; rolling behavior is pinned by tests (`FileRollingTests`).
- Deleted log segments are not chains-compatible by design: if you must delete aged logs, delete whole files and treat each file as an independent chain (each file starts its own chain at sequence 0).

## Routing into host logging

Use `Sink: MemoryLogSinkKind.Injected` plus `MemoryServiceBuilder.WithLogSink(...)` to forward `MemoryLogEntry` objects into your own logging pipeline. Note that hash-chain integrity applies to entries as emitted; if you need the chain, keep the file sink enabled as well.
