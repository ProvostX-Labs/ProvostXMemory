using System;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Reranking;

using Xunit;

namespace ProvostXMemory.RerankingTests;

/// <summary>IClientBackedReranker adapter: id mapping, drops, fallback merge, unknown/duplicate ids.</summary>
public sealed class ClientBackedRerankerTests
{
    [Fact]
    public async Task Client_order_maps_back_to_candidates()
    {
        var a = Fixtures.Rec("A", 0.9);
        var b = Fixtures.Rec("B", 0.8);
        var c = Fixtures.Rec("C", 0.7);
        var adapter = new ClientBackedReranker(new StubRerankerClient("C", "A", "B"));

        var reranked = await adapter.RerankAsync(Fixtures.Query("q"), new[] { a, b, c });

        Assert.Equal(new[] { "C", "A", "B" }, reranked.Select(r => r.Record.Id));
        // Wrapped fused scores and provenance are preserved (client ranks, doesn't rescore).
        Assert.Equal(0.7, reranked[0].Score, 9);
        Assert.Equal("test.fusion", reranked[0].Provenance.Retriever);
    }

    [Fact]
    public async Task TrustClientCompletely_drops_unlisted_candidates()
    {
        var a = Fixtures.Rec("A", 0.9);
        var b = Fixtures.Rec("B", 0.8);
        var adapter = new ClientBackedReranker(
            new StubRerankerClient("B"),
            options: new ClientBackedRerankerOptions(TrustClientCompletely: true));

        var reranked = await adapter.RerankAsync(Fixtures.Query("q"), new[] { a, b });

        Assert.Equal(new[] { "B" }, reranked.Select(r => r.Record.Id));
    }

    [Fact]
    public async Task Unlisted_candidates_fall_back_behind_client_ranked_when_not_trusting()
    {
        var a = Fixtures.Rec("A", 0.9);
        var b = Fixtures.Rec("B", 0.8);
        var c = Fixtures.Rec("C", 0.7);
        var adapter = new ClientBackedReranker(
            new StubRerankerClient("C"),
            options: new ClientBackedRerankerOptions(TrustClientCompletely: false));

        var reranked = await adapter.RerankAsync(Fixtures.Query("q"), new[] { a, b, c });

        Assert.Equal(new[] { "C", "A", "B" }, reranked.Select(r => r.Record.Id));
    }

    [Fact]
    public async Task Fallback_reranker_can_reorder_unlisted_candidates()
    {
        var a = Fixtures.Rec("A", 0.45, tags: new[] { "redis" });
        var b = Fixtures.Rec("B", 0.50);
        var c = Fixtures.Rec("C", 0.70);
        var adapter = new ClientBackedReranker(
            new StubRerankerClient("C"),
            fallback: new ScoreBoostReranker(),
            options: new ClientBackedRerankerOptions(TrustClientCompletely: false));

        var reranked = await adapter.RerankAsync(Fixtures.Query("redis"), new[] { a, b, c });

        // C first (client), then fallback boosts A above B.
        Assert.Equal(new[] { "C", "A", "B" }, reranked.Select(r => r.Record.Id));
    }

    [Fact]
    public async Task Unknown_client_ids_are_ignored()
    {
        var a = Fixtures.Rec("A", 0.9);
        var adapter = new ClientBackedReranker(new StubRerankerClient("GHOST", "A"));

        var reranked = await adapter.RerankAsync(Fixtures.Query("q"), new[] { a });

        Assert.Equal(new[] { "A" }, reranked.Select(r => r.Record.Id));
        Assert.Single(reranked);
    }

    [Fact]
    public async Task Duplicate_client_ids_collapse_to_first_mention()
    {
        var a = Fixtures.Rec("A", 0.9);
        var adapter = new ClientBackedReranker(new StubRerankerClient("A", "A"));

        var reranked = await adapter.RerankAsync(Fixtures.Query("q"), new[] { a });

        Assert.Single(reranked);
    }

    [Fact]
    public async Task Empty_client_reply_with_fallback_keeps_all_candidates()
    {
        var a = Fixtures.Rec("A", 0.9);
        var b = Fixtures.Rec("B", 0.5);
        var adapter = new ClientBackedReranker(
            new StubRerankerClient(),
            options: new ClientBackedRerankerOptions(TrustClientCompletely: false));

        var reranked = await adapter.RerankAsync(Fixtures.Query("q"), new[] { a, b });

        Assert.Equal(new[] { "A", "B" }, reranked.Select(r => r.Record.Id));
    }

    [Fact]
    public async Task Client_receives_candidate_ids_in_input_order()
    {
        var a = Fixtures.Rec("A", 0.9);
        var b = Fixtures.Rec("B", 0.8);
        var client = new StubRerankerClient("B", "A");
        var adapter = new ClientBackedReranker(client);

        await adapter.RerankAsync(Fixtures.Query("hello world"), new[] { a, b });

        Assert.Equal(1, client.Calls);
        Assert.Equal(new[] { "A", "B" }, client.Requests[0]);
        Assert.Equal("hello world", client.LastQueryText);
    }

    [Fact]
    public async Task Null_arguments_throw()
    {
        var adapter = new ClientBackedReranker(new StubRerankerClient("A"));
        var candidates = new[] { Fixtures.Rec("A", 0.5) };

        await Assert.ThrowsAsync<ArgumentNullException>(() => adapter.RerankAsync(null!, candidates));
        await Assert.ThrowsAsync<ArgumentNullException>(() => adapter.RerankAsync(Fixtures.Query("q"), null!));
        Assert.Throws<ArgumentNullException>(() => new ClientBackedReranker(null!));
    }
}