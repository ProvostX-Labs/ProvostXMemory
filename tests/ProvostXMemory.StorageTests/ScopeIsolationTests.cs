using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ProvostXMemory.Abstractions;
using ProvostXMemory.Storage;
using Xunit;

namespace ProvostXMemory.StorageTests;

/// <summary>Mandatory per ARCHITECTURE.md: cross-scope access must fail structurally.</summary>
public sealed class ScopeIsolationTests : IDisposable
{
    private readonly string _root;
    private readonly JsonlMemoryStore _store;

    public ScopeIsolationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "pvx-isolation-tests", Guid.NewGuid().ToString("N"));
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

    private static MemoryRecord Record(string kind, string scopeId, string id, string content)
    {
        var kindParsed = Enum.TryParse<MemoryScopeKind>(kind, ignoreCase: true, out var parsed)
            ? parsed
            : MemoryScopeKind.Custom;
        return new MemoryRecord(id, new MemoryScope(kindParsed, scopeId), Key: $"{kind}:{scopeId}:{id}", content);
    }

    [Fact]
    public async Task Two_scopes_never_see_each_others_records()
    {
        await _store.AddAsync(Record("Tenant", "acme", "secret-1", "acme private"));
        await _store.AddAsync(Record("Tenant", "globex", "secret-2", "globex private"));

        var acmeList = await _store.ListAsync("Tenant", "acme");
        var globexList = await _store.ListAsync("Tenant", "globex");

        Assert.Single(acmeList);
        Assert.Single(globexList);
        Assert.Equal("secret-1", acmeList[0].Id);
        Assert.Equal("secret-2", globexList[0].Id);

        // Cross-scope gets must not find the other scope's record.
        Assert.Null(await _store.GetAsync("Tenant", "acme", "secret-2"));
        Assert.Null(await _store.GetAsync("Tenant", "globex", "secret-1"));

        Assert.Equal(1, await _store.CountAsync("Tenant", "acme"));
        Assert.Equal(1, await _store.CountAsync("Tenant", "globex"));
    }

    [Fact]
    public async Task Same_id_in_different_scopes_is_allowed_and_isolated()
    {
        await _store.AddAsync(Record("Tenant", "acme", "shared-id", "acme content"));
        await _store.AddAsync(Record("Session", "acme", "shared-id", "session content"));

        Assert.Equal("acme content", (await _store.GetAsync("Tenant", "acme", "shared-id"))!.Content);
        Assert.Equal("session content", (await _store.GetAsync("Session", "acme", "shared-id"))!.Content);
    }

    [Fact]
    public async Task Isolation_exists_on_disk()
    {
        await _store.AddAsync(Record("Tenant", "acme", "disk-1", "acme"));
        await _store.AddAsync(Record("Tenant", "globex", "disk-2", "globex"));

        var acmeFile = _store.Layout.RecordsPath("Tenant", "acme");
        var globexFile = _store.Layout.RecordsPath("Tenant", "globex");

        Assert.NotEqual(Path.GetDirectoryName(acmeFile), Path.GetDirectoryName(globexFile));
        Assert.Contains("disk-1", await File.ReadAllTextAsync(acmeFile));
        Assert.DoesNotContain("disk-2", await File.ReadAllTextAsync(acmeFile));
        Assert.DoesNotContain("disk-1", await File.ReadAllTextAsync(globexFile));
    }

    [Fact]
    public async Task Scope_paths_of_different_scopes_never_collide()
    {
        var layout = new MemoryRootLayout(_root, "model-h4sh");

        var first = layout.RecordsPath("Tenant", "acme");
        var second = layout.RecordsPath("Tenant", "globex");
        var third = layout.RecordsPath("Session", "acme");

        Assert.Equal(3, new[] { first, second, third }.Distinct(StringComparer.Ordinal).Count());
        Assert.StartsWith(Path.GetFullPath(_root), first, StringComparison.Ordinal);
        Assert.StartsWith(Path.GetFullPath(_root), second, StringComparison.Ordinal);
        Assert.StartsWith(Path.GetFullPath(_root), third, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Parent_scope_does_not_leak_into_child_reads()
    {
        var parentScope = new MemoryScope(MemoryScopeKind.Tenant, "acme");
        var childScope = new MemoryScope(MemoryScopeKind.Session, "conv-1", parentScope);

        await _store.AddAsync(new MemoryRecord("parent-rec", parentScope, "p", "tenant-level"));
        await _store.AddAsync(new MemoryRecord("child-rec", childScope, "c", "session-level"));

        var childList = await _store.ListAsync("Session", "conv-1");
        var parentList = await _store.ListAsync("Tenant", "acme");

        Assert.Single(childList);
        Assert.Single(parentList);
        Assert.Equal("child-rec", childList[0].Id);
        Assert.Equal("parent-rec", parentList[0].Id);
    }
}