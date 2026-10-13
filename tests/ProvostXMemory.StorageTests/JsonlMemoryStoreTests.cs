using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Storage;
using Xunit;

namespace ProvostXMemory.StorageTests;

public sealed class JsonlMemoryStoreTests : IDisposable
{
    private readonly string _root;
    private readonly JsonlMemoryStore _store;

    public JsonlMemoryStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "pvx-store-tests", Guid.NewGuid().ToString("N"));
        _store = new JsonlMemoryStore(new MemoryRootLayout(_root, "model-h4sh"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private static MemoryRecord Record(string scopeId, string id, string content, MemoryScopeKind kind = MemoryScopeKind.Tenant)
        => new(id, new MemoryScope(kind, scopeId), Key: $"{scopeId}:{id}", content);

    [Fact]
    public async Task Add_then_Get_round_trips_persisted_fields()
    {
        var expires = DateTimeOffset.UtcNow.AddDays(3);
        var original = new MemoryRecord(
            "rec-1", new MemoryScope(MemoryScopeKind.Tenant, "acme"), "k1", "hello",
            Category: "notes",
            Tags: new[] { "a", "b" })
        {
            ExpiresAt = expires,
        };

        await _store.AddAsync(original);

        var loaded = await _store.GetAsync("Tenant", "acme", "rec-1");
        Assert.NotNull(loaded);
        Assert.Equal(original.Id, loaded!.Id);
        Assert.Equal(original.Scope.Kind, loaded.Scope.Kind);
        Assert.Equal(original.Scope.Id, loaded.Scope.Id);
        Assert.Equal(original.Key, loaded.Key);
        Assert.Equal(original.Content, loaded.Content);
        Assert.Equal(original.Category, loaded.Category);
        Assert.Equal(original.Tags, loaded.Tags);
        Assert.Equal(expires.ToUnixTimeMilliseconds(), loaded.ExpiresAt!.Value.ToUnixTimeMilliseconds());
        Assert.Equal(original.CreatedAt.ToUnixTimeMilliseconds(), loaded.CreatedAt.ToUnixTimeMilliseconds());
        // Records without Reps/Meta still degrade to empty defaults (never null).
        Assert.Empty(loaded.Reps);
        Assert.Empty(loaded.Meta);
    }

    [Fact]
    public async Task Reps_and_Meta_survive_full_write_read_cycle()
    {
        var original = new MemoryRecord(
            "rec-reps",
            new MemoryScope(MemoryScopeKind.Tenant, "acme"),
            "k-reps",
            "main content")
        {
            Reps = new[]
            {
                new MemoryRepresentation(MemoryRepresentationModality.Text, "text rep"),
                new MemoryRepresentation(
                    MemoryRepresentationModality.Image,
                    "img rep",
                    EmbeddingModelHash: "img-hash-1",
                    Properties: new Dictionary<string, string> { ["width"] = "64" }),
            },
            Meta = new Dictionary<string, string> { ["cluster-summary"] = "true", ["cluster-id"] = "abc" },
        };

        await _store.AddAsync(original);

        // A second store instance over the same root proves persistence, not cache luck.
        var reloadedStore = new JsonlMemoryStore(new MemoryRootLayout(_root, "model-h4sh"));
        var loaded = await reloadedStore.GetAsync("Tenant", "acme", "rec-reps");
        Assert.NotNull(loaded);
        Assert.Equal(original.Reps.Count, loaded!.Reps.Count);
        Assert.Equal(original.Reps[0].Modality, loaded.Reps[0].Modality);
        Assert.Equal(original.Reps[0].Content, loaded.Reps[0].Content);
        Assert.Equal(original.Reps[1].Modality, loaded.Reps[1].Modality);
        Assert.Equal(original.Reps[1].Content, loaded.Reps[1].Content);
        Assert.Equal(original.Reps[1].EmbeddingModelHash, loaded.Reps[1].EmbeddingModelHash);
        Assert.Equal("64", loaded.Reps[1].Properties["width"]);
        Assert.Equal(original.Meta.OrderBy(kv => kv.Key), loaded.Meta.OrderBy(kv => kv.Key));

        // The on-disk JSONL line carries the persisted fields.
        var line = (await File.ReadAllLinesAsync(_store.Layout.RecordsPath("Tenant", "acme")))
            .Single(l => l.Contains("\"rec-reps\""));
        Assert.Contains("\"reps\"", line);
        Assert.Contains("\"meta\"", line);
        Assert.Contains("img-hash-1", line);
    }

    [Fact]
    public async Task Meta_only_record_survives_without_reps()
    {
        var original = new MemoryRecord(
            "rec-meta-only",
            new MemoryScope(MemoryScopeKind.Tenant, "acme"),
            "k-meta",
            "content")
        {
            Meta = new Dictionary<string, string> { ["consolidation-report"] = "{}" },
        };

        await _store.AddAsync(original);
        var loaded = await _store.GetAsync("Tenant", "acme", "rec-meta-only");
        Assert.NotNull(loaded);
        Assert.Empty(loaded!.Reps); // empty, not null
        Assert.Equal(original.Meta, loaded.Meta);
    }

    [Fact]
    public async Task Legacy_entry_without_reps_and_meta_deserializes_to_empty()
    {
        // Hand-written pre-wave-4d line: no reps/meta properties at all.
        var legacyJson =
            "{\"id\":\"legacy-1\",\"scopeKind\":\"Tenant\",\"scopeId\":\"acme\",\"key\":\"old\"," +
            "\"content\":\"old content\",\"category\":null,\"tags\":[\"t\"]," +
            "\"createdAtUnixMs\":1234567890,\"updatedAtUnixMs\":1234567890,\"expiresAtUnixMs\":null}";
        var path = _store.Layout.RecordsPath("Tenant", "acme");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, legacyJson + "\n");

        var loaded = await _store.GetAsync("Tenant", "acme", "legacy-1");
        Assert.NotNull(loaded);
        Assert.Equal("old content", loaded!.Content);
        Assert.Empty(loaded.Reps); // empty, not null crash
        Assert.Empty(loaded.Meta);

        // Re-saving the legacy entry through the new writer normalizes it without loss.
        loaded = loaded with { Content = "updated content" };
        await _store.UpdateAsync(loaded);
        var roundTripped = await new JsonlMemoryStore(new MemoryRootLayout(_root, "model-h4sh")).GetAsync("Tenant", "acme", "legacy-1");
        Assert.NotNull(roundTripped);
        Assert.Equal("updated content", roundTripped!.Content);
        Assert.Empty(roundTripped.Reps);
        Assert.Empty(roundTripped.Meta);
        Assert.Equal(new[] { "t" }, roundTripped.Tags);
    }

    [Fact]
    public async Task Entry_model_round_trips_reps_and_meta_through_ReadEntry()
    {
        var reps = new List<MemoryRepresentation>
        {
            new(MemoryRepresentationModality.Table, "table rep", null, new Dictionary<string, string> { ["rows"] = "3" }),
        };
        var meta = new Dictionary<string, string> { ["k"] = "v" };
        var entry = new MemoryEntryModel(
            "e1", "Tenant", "acme", "k", "content", null,
            new List<string> { "tag" }, 1234567890, 1234567891, null, reps, meta);

        await _store.WriteEntryAsync("Tenant", "acme", entry);
        var read = await _store.ReadEntryAsync("Tenant", "acme", "e1");

        Assert.NotNull(read);
        Assert.Equal(entry.Id, read!.Id);
        Assert.NotNull(read.Reps);
        var rep = Assert.Single(read.Reps);
        Assert.Equal(reps[0].Modality, rep.Modality);
        Assert.Equal(reps[0].Content, rep.Content);
        Assert.Equal("3", rep.Properties["rows"]);
        Assert.NotNull(read.Meta);
        Assert.Equal(entry.Meta, read.Meta);
    }

    [Fact]
    public async Task Add_duplicate_id_in_same_scope_throws()
    {
        await _store.AddAsync(Record("acme", "rec-1", "first"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.AddAsync(Record("acme", "rec-1", "dup")));
    }

    [Fact]
    public async Task Update_replaces_content_and_keeps_single_line()
    {
        await _store.AddAsync(Record("acme", "rec-1", "before"));
        var updated = Record("acme", "rec-1", "after");
        await _store.UpdateAsync(updated);

        var loaded = await _store.GetAsync("Tenant", "acme", "rec-1");
        Assert.NotNull(loaded);
        Assert.Equal("after", loaded!.Content);

        var lines = (await File.ReadAllLinesAsync(_store.Layout.RecordsPath("Tenant", "acme")))
            .Count(line => !string.IsNullOrWhiteSpace(line));
        Assert.Equal(1, lines);
    }

    [Fact]
    public async Task Update_missing_record_throws()
    {
        await Assert.ThrowsAnyAsync<KeyNotFoundException>(() => _store.UpdateAsync(Record("acme", "ghost", "x")));
    }

    [Fact]
    public async Task Delete_is_idempotent()
    {
        await _store.AddAsync(Record("acme", "rec-1", "x"));
        await _store.DeleteAsync("Tenant", "acme", "rec-1");
        await _store.DeleteAsync("Tenant", "acme", "rec-1"); // no-op, no throw

        Assert.Null(await _store.GetAsync("Tenant", "acme", "rec-1"));
        Assert.Equal(0, await _store.CountAsync("Tenant", "acme"));
    }

    [Fact]
    public async Task List_Count_Snapshot_agree()
    {
        await _store.AddAsync(Record("acme", "r1", "a"));
        await _store.AddAsync(Record("acme", "r2", "b"));
        await _store.AddAsync(Record("acme", "r3", "c"));

        var list = await _store.ListAsync("Tenant", "acme");
        var snapshot = await _store.SnapshotAsync("Tenant", "acme");
        var count = await _store.CountAsync("Tenant", "acme");

        Assert.Equal(3, count);
        Assert.Equal(3, list.Count);
        Assert.Equal(snapshot.Select(r => r.Id), list.Select(r => r.Id));
    }

    [Fact]
    public async Task Empty_or_missing_scope_reads_as_empty()
    {
        Assert.Equal(0, await _store.CountAsync("Tenant", "never-created"));
        Assert.Empty(await _store.ListAsync("Tenant", "never-created"));
        Assert.Empty(await _store.SnapshotAsync("Tenant", "never-created"));
        Assert.Null(await _store.GetAsync("Tenant", "never-created", "any"));
    }

    [Fact]
    public async Task WriteEntry_then_ReadEntry_round_trips()
    {
        // STJ materializes IReadOnlyList<string> as List<string>; construct matching shape for record equality.
        var entry = new MemoryEntryModel(
            "e1", "Tenant", "acme", "k", "content", null,
            new System.Collections.Generic.List<string> { "tag" }, 1234567890, 1234567891, null);

        await _store.WriteEntryAsync("Tenant", "acme", entry);
        var read = await _store.ReadEntryAsync("Tenant", "acme", "e1");

        Assert.NotNull(read);
        Assert.Equal(entry.Id, read!.Id);
        Assert.Equal(entry.ScopeKind, read.ScopeKind);
        Assert.Equal(entry.ScopeId, read.ScopeId);
        Assert.Equal(entry.Key, read.Key);
        Assert.Equal(entry.Content, read.Content);
        Assert.Equal(entry.Category, read.Category);
        Assert.Equal(entry.Tags, read.Tags);
        Assert.Equal(entry.CreatedAtUnixMs, read.CreatedAtUnixMs);
        Assert.Equal(entry.UpdatedAtUnixMs, read.UpdatedAtUnixMs);
        Assert.Equal(entry.ExpiresAtUnixMs, read.ExpiresAtUnixMs);
    }

    [Fact]
    public async Task Store_persists_across_instances()
    {
        await _store.AddAsync(Record("acme", "persist-1", "durable"));

        var secondStore = new JsonlMemoryStore(new MemoryRootLayout(_root, "model-h4sh"));
        var loaded = await secondStore.GetAsync("Tenant", "acme", "persist-1");
        Assert.NotNull(loaded);
        Assert.Equal("durable", loaded!.Content);
    }

    [Fact]
    public async Task Scope_parameters_are_required()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _store.ListAsync("", "acme"));
        await Assert.ThrowsAsync<ArgumentException>(() => _store.ListAsync("Tenant", " "));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.GetAsync("Tenant", "acme", null!));
    }
}