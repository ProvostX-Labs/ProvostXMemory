using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory.VectorIndex;

using Xunit;

namespace ProvostXMemory.VectorIndexTests;

/// <summary>FlatJson backend specifics: persistence round-trip, guards, atomic swap.</summary>
public sealed class FlatJsonVectorIndexTests
{
    private readonly string _tempDir;

    public FlatJsonVectorIndexTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("pvx-vecidx-flatjson-t-").FullName;
    }

    private static VectorItem Item(string id, params float[] coords) => new(id, coords);

    private static float[] Vec(int seed, int dim = 8)
    {
        var vector = new float[dim];
        for (var d = 0; d < dim; d++)
        {
            vector[d] = MathF.Sin((seed + 1) * (d + 2) * 0.71f);
        }

        return vector;
    }

    private FlatJsonVectorIndex Create(string fileName)
    {
        var path = Path.Combine(_tempDir, fileName);
        return new FlatJsonVectorIndex(
            new VectorNamespaceFile(path, "hash-A", 8),
            new VectorIndexGuard("hash-A", 8));
    }

    [Fact]
    public async Task Flush_then_reload_preserves_search_results()
    {
        const string name = "persist.json";
        var corpus = Enumerable.Range(0, 25)
            .Select(i => Item($"id-{i:D2}", Vec(i)))
            .ToList();

        var first = Create(name);
        await first.BuildAsync(corpus);
        var expected = await first.SearchAsync(Vec(3), 5);
        await first.FlushAsync();

        var second = Create(name);
        Assert.Equal(first.Count, second.Count);
        var actual = await second.SearchAsync(Vec(3), 5);

        Assert.Equal(expected.Select(static hit => hit.Id), actual.Select(static hit => hit.Id));
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Score, actual[i].Score, 4f);
        }
    }

    [Fact]
    public async Task Flush_writes_file_with_no_temp_leftovers()
    {
        var index = Create("atomic.json");
        await index.BuildAsync(new[] { Item("a", Vec(0)), Item("b", Vec(1)) });
        await index.FlushAsync();

        var files = Directory.GetFiles(_tempDir).Select(Path.GetFileName).ToList();
        Assert.Contains("atomic.json", files);
        Assert.DoesNotContain(files, static name => name!.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Open_rejects_wrong_model_hash_file()
    {
        const string name = "guard.json";
        var index = Create(name);
        await index.BuildAsync(new[] { Item("a", Vec(0)) });
        await index.FlushAsync();

        // same dim, wrong model hash → constructor must throw guard mismatch
        var wrongHash = new VectorNamespaceFile(Path.Combine(_tempDir, name), "hash-B", 8);
        Assert.Throws<VectorIndexGuardMismatchException>(
            () => new FlatJsonVectorIndex(wrongHash, new VectorIndexGuard("hash-B", 8)));
    }

    [Fact]
    public async Task Open_rejects_wrong_dim_file()
    {
        const string name = "guard-dim.json";
        var index = Create(name);
        await index.BuildAsync(new[] { Item("a", Vec(0)) });
        await index.FlushAsync();

        var wrongDim = new VectorNamespaceFile(Path.Combine(_tempDir, name), "hash-A", 4);
        Assert.Throws<VectorIndexGuardMismatchException>(
            () => new FlatJsonVectorIndex(wrongDim, new VectorIndexGuard("hash-A", 4)));
    }

    [Fact]
    public void Corrupt_file_rejected_with_format_error()
    {
        const string name = "corrupt.json";
        File.WriteAllText(Path.Combine(_tempDir, name), "{ not valid json !!!");
        Assert.Throws<InvalidOperationException>(() => Create(name));
    }

    [Fact]
    public void Unknown_file_format_rejected()
    {
        const string name = "foreign.json";
        File.WriteAllText(Path.Combine(_tempDir, name), "{\"fileId\":\"someone-else-v7\",\"items\":[]}");
        Assert.Throws<InvalidOperationException>(() => Create(name));
    }

    [Fact]
    public async Task Unflushed_build_is_invisible_on_reload()
    {
        const string name = "dirty.json";
        var index = Create(name);
        await index.BuildAsync(new[] { Item("a", Vec(0)) });
        await index.FlushAsync();

        await index.AddAsync(Item("b", Vec(1))); // not flushed
        var revived = Create(name);
        Assert.Equal(1, revived.Count);
    }

    [Fact]
    public async Task Dirty_flag_tracks_unflushed_mutations()
    {
        var index = Create("dirty-flag.json");
        Assert.False(index.HasUnflushedChanges);

        await index.AddAsync(Item("a", Vec(0)));
        Assert.True(index.HasUnflushedChanges);

        await index.FlushAsync();
        Assert.False(index.HasUnflushedChanges);
    }
}