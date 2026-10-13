using System;
using System.IO;
using Xunit;
using ProvostXMemory.Storage;

namespace ProvostXMemory.StorageTests;

public sealed class MemoryRootLayoutTests
{
    private static MemoryRootLayout CreateLayout(string root)
        => new(root, "model-h4sh");

    [Theory]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("con")]
    [InlineData("nul")]
    [InlineData("com1")]
    [InlineData(".hidden")]
    [InlineData("id.")]
    [InlineData("id\n")]
    public void ScopePath_rejects_unsafe_segments(string scopeId)
    {
        var layout = CreateLayout(Path.Combine(Path.GetTempPath(), "root"));
        Assert.ThrowsAny<ArgumentException>(() => layout.ScopePath("tenant", scopeId));
    }

    [Fact]
    public void Null_segments_are_rejected()
    {
        var layout = CreateLayout(Path.Combine(Path.GetTempPath(), "root"));
        Assert.Throws<ArgumentNullException>(() => layout.ScopePath(null!, "id"));
        Assert.Throws<ArgumentNullException>(() => layout.ScopePath("tenant", null!));
        Assert.Throws<ArgumentNullException>(() => layout.VectorsPath("tenant", "id", null!));
    }

    [Fact]
    public void ScopePath_resolves_under_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "scope-path-ok");
        var layout = CreateLayout(root);
        var path = layout.ScopePath("tenant", "acme");
        Assert.StartsWith(Path.GetFullPath(root), path, StringComparison.Ordinal);
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "tenant", "acme"), path);
    }

    [Fact]
    public void Layout_paths_match_expected_shape()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "layout-shape"));
        var layout = CreateLayout(root);

        Assert.Equal(Path.Combine(root, "manifest.json"), layout.ManifestPath);
        Assert.Equal(Path.Combine(root, "tenant", "acme", "records.jsonl"), layout.RecordsPath("tenant", "acme"));
        Assert.Equal(Path.Combine(root, "tenant", "acme", "chunks"), layout.ChunksPath("tenant", "acme"));
        Assert.Equal(Path.Combine(root, "tenant", "acme", "vectors", "model-h4sh"), layout.VectorsPath("tenant", "acme", "model-h4sh"));
        Assert.Equal(Path.Combine(root, "tenant", "acme", "graph", "entities.jsonl"), layout.GraphPath("tenant", "acme", "entities.jsonl"));
        Assert.Equal(Path.Combine(root, "tenant", "acme", "model-h4sh"), layout.ScopePath("tenant", "acme", "model-h4sh"));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("a/b")]
    public void Layout_rejects_traversal_in_any_segment(string modelHash)
    {
        var layout = CreateLayout(Path.Combine(Path.GetTempPath(), "layout-traversal"));
        Assert.ThrowsAny<ArgumentException>(() => layout.VectorsPath("tenant", "acme", modelHash));
        Assert.ThrowsAny<ArgumentException>(() => layout.GraphPath("tenant", "acme", modelHash));
    }

    [Fact]
    public void Constructor_rejects_unsafe_default_model_hash()
    {
        Assert.ThrowsAny<ArgumentException>(() => new MemoryRootLayout(Path.GetTempPath(), "../model"));
        Assert.ThrowsAny<ArgumentException>(() => new MemoryRootLayout(Path.GetTempPath(), ""));
    }

    [Fact]
    public void Constructor_rejects_empty_root()
    {
        Assert.ThrowsAny<ArgumentException>(() => new MemoryRootLayout("", "model"));
    }

    [Fact]
    public void GraphPath_rejects_non_filename_values()
    {
        var layout = CreateLayout(Path.Combine(Path.GetTempPath(), "layout-graph"));
        Assert.ThrowsAny<ArgumentException>(() => layout.GraphPath("tenant", "acme", "sub/dir.jsonl"));
        Assert.ThrowsAny<ArgumentException>(() => layout.GraphPath("tenant", "acme", "entities.jsonl/../relations.jsonl"));
    }
}