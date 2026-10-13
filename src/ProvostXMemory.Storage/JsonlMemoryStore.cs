using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Storage;

/// <summary>
/// Default JSONL-backed store: one append/rewrite-friendly <c>records.jsonl</c> per
/// scope, one JSON document per line, transactional rewrite on every mutation.
/// All operations take the scope as a required parameter and resolve paths only
/// through <see cref="MemoryRootLayout"/>, so cross-scope access is structurally impossible.
/// </summary>
public sealed class JsonlMemoryStore : IJsonlMemoryStore
{
    // Pure factory of JSON serializer options; immutable after creation, no shared mutable state.
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private const string RecordsFileName = "records.jsonl";

    private readonly MemoryRootLayout _layout;
    private readonly AtomicFileWriter _writer;

    public JsonlMemoryStore(MemoryRootLayout layout, AtomicFileWriter? writer = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _layout = layout;
        _writer = writer ?? new AtomicFileWriter(fsync: false); // pure factory default; no shared mutable state
    }

    public MemoryRootLayout Layout => _layout;

    public async Task AddAsync(MemoryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        var recordsPath = _layout.RecordsPath(record.Scope.Kind.ToString(), record.Scope.Id);
        var entries = await ReadAllEntriesAsync(recordsPath, cancellationToken).ConfigureAwait(false);

        if (entries.Any(entry => string.Equals(entry.Id, record.Id, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("A record with the same id already exists in this scope.");
        }

        var entry = ToEntry(record);
        await WriteAllEntriesAsync(recordsPath, entries.Append(entry), cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        var recordsPath = _layout.RecordsPath(record.Scope.Kind.ToString(), record.Scope.Id);
        var entries = await ReadAllEntriesAsync(recordsPath, cancellationToken).ConfigureAwait(false);

        var index = FindIndexById(entries, record.Id);
        if (index < 0)
        {
            throw new KeyNotFoundException("No record with the given id exists in this scope.");
        }

        entries.RemoveAt(index);
        entries.Insert(index, ToEntry(record));
        await WriteAllEntriesAsync(recordsPath, entries, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string scopeKind, string scopeId, string id, CancellationToken cancellationToken = default)
    {
        ValidateScopeParameters(scopeKind, scopeId);
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        var recordsPath = _layout.RecordsPath(scopeKind, scopeId);
        var entries = await ReadAllEntriesAsync(recordsPath, cancellationToken).ConfigureAwait(false);

        var index = FindIndexById(entries, id);
        if (index < 0)
        {
            return; // delete is idempotent
        }

        entries.RemoveAt(index);
        await WriteAllEntriesAsync(recordsPath, entries, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MemoryRecord?> GetAsync(string scopeKind, string scopeId, string id, CancellationToken cancellationToken = default)
    {
        ValidateScopeParameters(scopeKind, scopeId);
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        var entry = await ReadEntryAsync(scopeKind, scopeId, id, cancellationToken).ConfigureAwait(false);
        return entry is null ? null : ToRecord(entry);
    }

    public Task<IReadOnlyList<MemoryRecord>> ListAsync(string scopeKind, string scopeId, CancellationToken cancellationToken = default)
        => SnapshotAsync(scopeKind, scopeId, cancellationToken);

    public async Task<long> CountAsync(string scopeKind, string scopeId, CancellationToken cancellationToken = default)
    {
        ValidateScopeParameters(scopeKind, scopeId);
        cancellationToken.ThrowIfCancellationRequested();

        var path = _layout.RecordsPath(scopeKind, scopeId);
        if (!File.Exists(path))
        {
            return 0;
        }

        var count = 0L;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1 << 16, leaveOpen: false);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                count++;
            }
        }

        return count;
    }

    public async Task<IReadOnlyList<MemoryRecord>> SnapshotAsync(string scopeKind, string scopeId, CancellationToken cancellationToken = default)
    {
        ValidateScopeParameters(scopeKind, scopeId);
        cancellationToken.ThrowIfCancellationRequested();

        var path = _layout.RecordsPath(scopeKind, scopeId);
        var entries = await ReadAllEntriesAsync(path, cancellationToken).ConfigureAwait(false);
        var records = new List<MemoryRecord>(entries.Count);
        foreach (var entry in entries)
        {
            records.Add(ToRecord(entry));
        }

        return records;
    }

    public async Task<MemoryEntryModel?> ReadEntryAsync(string scopeKind, string scopeId, string id, CancellationToken cancellationToken = default)
    {
        ValidateScopeParameters(scopeKind, scopeId);
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        var path = _layout.RecordsPath(scopeKind, scopeId);
        var entries = await ReadAllEntriesAsync(path, cancellationToken).ConfigureAwait(false);
        return entries.FirstOrDefault(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
    }

    public async Task WriteEntryAsync(string scopeKind, string scopeId, MemoryEntryModel entry, CancellationToken cancellationToken = default)
    {
        ValidateScopeParameters(scopeKind, scopeId);
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        var path = _layout.RecordsPath(scopeKind, scopeId);
        var entries = await ReadAllEntriesAsync(path, cancellationToken).ConfigureAwait(false);

        var index = FindIndexById(entries, entry.Id);
        if (index >= 0)
        {
            entries.RemoveAt(index);
        }

        entries.Add(entry);
        await WriteAllEntriesAsync(path, entries, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<MemoryEntryModel>> ReadAllEntriesAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new List<MemoryEntryModel>();
        }

        var entries = new List<MemoryEntryModel>();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1 << 16);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize<MemoryEntryModel>(line, JsonOptions);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    private async Task WriteAllEntriesAsync(string path, IEnumerable<MemoryEntryModel> entries, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _writer.CleanupStaleTempFiles(path);

        var builder = new StringBuilder();
        foreach (var entry in entries)
        {
            builder.Append(JsonSerializer.Serialize(entry, JsonOptions));
            builder.Append('\n');
        }

        cancellationToken.ThrowIfCancellationRequested();
        _writer.Write(path, builder.ToString());
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static int FindIndexById(List<MemoryEntryModel> entries, string id)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static void ValidateScopeParameters(string scopeKind, string scopeId)
    {
        if (string.IsNullOrWhiteSpace(scopeKind))
        {
            throw new ArgumentException("Scope kind is required.", nameof(scopeKind));
        }

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            throw new ArgumentException("Scope id is required.", nameof(scopeId));
        }
    }

    // Pure mapping functions between the in-memory record shape and the on-disk entry shape.
    // Reps/Meta are written as optional trailing fields; legacy entries without them deserialize
    // with null and map to the record's empty defaults (backward compatible, not null-crashing).
    private static MemoryEntryModel ToEntry(MemoryRecord record) => new(
        Id: record.Id,
        ScopeKind: record.Scope.Kind.ToString(),
        ScopeId: record.Scope.Id,
        Key: record.Key,
        Content: record.Content,
        Category: record.Category,
        Tags: record.Tags,
        CreatedAtUnixMs: record.CreatedAt.ToUnixTimeMilliseconds(),
        UpdatedAtUnixMs: record.UpdatedAt.ToUnixTimeMilliseconds(),
        ExpiresAtUnixMs: record.ExpiresAt?.ToUnixTimeMilliseconds(),
        Reps: record.Reps,
        Meta: DictionaryOrNull(record.Meta));

    private static MemoryRecord ToRecord(MemoryEntryModel entry)
    {
        var scope = new MemoryScope(
            ParseScopeKind(entry.ScopeKind),
            entry.ScopeId);
        return new MemoryRecord(
            Id: entry.Id,
            Scope: scope,
            Key: entry.Key,
            Content: entry.Content,
            Category: entry.Category)
        {
            Tags = RequireNonNull(entry.Tags, static () => Array.Empty<string>()),
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(entry.CreatedAtUnixMs),
            UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(entry.UpdatedAtUnixMs),
            ExpiresAt = entry.ExpiresAtUnixMs.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(entry.ExpiresAtUnixMs.Value)
                : null,
            Reps = RequireNonNull(entry.Reps, static () => Array.Empty<MemoryRepresentation>()),
            Meta = RequireNonNull(entry.Meta, static () => new Dictionary<string, string>()),
        };
    }

    // Pure guard: absent/empty Meta is written as an absent field, keeping legacy lines byte-compatible.
    private static IReadOnlyDictionary<string, string>? DictionaryOrNull(IReadOnlyDictionary<string, string> meta)
        => meta.Count == 0 ? null : meta;

    // Pure guard: null (absent field) maps to the given empty default instead of a null crash.
    private static IReadOnlyList<T> RequireNonNull<T>(IReadOnlyList<T>? value, Func<IReadOnlyList<T>> empty)
        => value ?? empty();

    private static IReadOnlyDictionary<string, string> RequireNonNull(IReadOnlyDictionary<string, string>? value, Func<IReadOnlyDictionary<string, string>> empty)
        => value ?? empty();

    // Pure parser for scope kinds; unknown values degrade to Custom.
    private static MemoryScopeKind ParseScopeKind(string value)
    {
        return Enum.TryParse<MemoryScopeKind>(value, ignoreCase: true, out var kind) ? kind : MemoryScopeKind.Custom;
    }

    // Pure factory: immutable serializer options (camelCase JSONL documents).
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        return options;
    }
}