using System;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Reranking;

using Xunit;

namespace ProvostXMemory.RerankingTests;

/// <summary>NoopReranker identity contract.</summary>
public sealed class NoopRerankerTests
{
    [Fact]
    public async Task Returns_candidates_identically_order_and_scores()
    {
        var candidates = new[]
        {
            Fixtures.Rec("A", 0.9),
            Fixtures.Rec("B", 0.1, tags: new[] { "whatever" }),
            Fixtures.Rec("C", 0.5, content: "x"),
        };
        var reranker = new NoopReranker();

        var reranked = await reranker.RerankAsync(Fixtures.Query("any query"), candidates);

        Assert.Equal(candidates.Select(c => c.Record.Id), reranked.Select(r => r.Record.Id));
        Assert.Equal(candidates.Select(c => c.Score), reranked.Select(r => r.Score));
        Assert.Equal(candidates.Select(c => c.Provenance.Retriever), reranked.Select(r => r.Provenance.Retriever));
    }

    [Fact]
    public async Task Empty_candidates_yield_empty_result()
    {
        var reranker = new NoopReranker();

        var reranked = await reranker.RerankAsync(
            Fixtures.Query("q"),
            Array.Empty<ScoredMemoryRecord>());

        Assert.Empty(reranked);
    }

    [Fact]
    public async Task Null_arguments_throw()
    {
        var reranker = new NoopReranker();
        var candidates = new[] { Fixtures.Rec("A", 0.5) };

        await Assert.ThrowsAsync<ArgumentNullException>(() => reranker.RerankAsync(null!, candidates));
        await Assert.ThrowsAsync<ArgumentNullException>(() => reranker.RerankAsync(Fixtures.Query("q"), null!));
    }
}