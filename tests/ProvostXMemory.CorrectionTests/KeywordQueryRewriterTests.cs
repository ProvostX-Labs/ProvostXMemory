using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Correction;

using Xunit;

namespace ProvostXMemory.CorrectionTests;

/// <summary>KeywordQueryRewriter: stopword stripping, acronym preservation, dedup, determinism.</summary>
public sealed class KeywordQueryRewriterTests
{
    [Fact]
    public async Task Strips_stopwords_and_keeps_key_terms()
    {
        var rewriter = new KeywordQueryRewriter();

        var rewritten = await rewriter.RewriteAsync(Fixtures.Query("what is the best way to store data"));

        Assert.Equal("best way store data", rewritten);
    }

    [Fact]
    public async Task Preserves_acronyms_case()
    {
        var rewriter = new KeywordQueryRewriter();

        var rewritten = await rewriter.RewriteAsync(Fixtures.Query("how does the HTTP API work with .NET and C#"));

        // "with" and "and" are stopwords; "HTTP", "API", ".NET", "C#" preserved verbatim;
        // "work" is a content word and stays.
        Assert.Equal("HTTP API work .NET C#", rewritten);
    }

    [Fact]
    public async Task Collapses_duplicate_terms_keeping_first_casing()
    {
        var rewriter = new KeywordQueryRewriter();

        var rewritten = await rewriter.RewriteAsync(Fixtures.Query("Cache cache CACHE Redis"));

        Assert.Equal("Cache Redis", rewritten);
    }

    [Fact]
    public async Task Empty_and_whitespace_queries_rewrite_to_empty_string()
    {
        var rewriter = new KeywordQueryRewriter();

        Assert.Equal(string.Empty, await rewriter.RewriteAsync(Fixtures.Query(string.Empty)));
        Assert.Equal(string.Empty, await rewriter.RewriteAsync(Fixtures.Query("   \t  ")));
    }

    [Fact]
    public async Task All_stopword_query_rewrites_to_empty_string()
    {
        var rewriter = new KeywordQueryRewriter();

        Assert.Equal(string.Empty, await rewriter.RewriteAsync(Fixtures.Query("the of and to is")));
    }

    [Fact]
    public async Task Deterministic_across_repeated_calls_and_instances()
    {
        var query = Fixtures.Query("Explain retrieval augmented generation with向量 store indexing");
        var first = new KeywordQueryRewriter();
        var second = new KeywordQueryRewriter();

        var a = await first.RewriteAsync(query);
        var b = await first.RewriteAsync(query);
        var c = await second.RewriteAsync(query);

        Assert.Equal(a, b);
        Assert.Equal(a, c);
        Assert.Equal(
            a,
            string.Join(' ', QueryTokens.KeyTerms(query.Text)));
    }

    [Fact]
    public async Task Word_order_and_first_appearance_are_stable()
    {
        var rewriter = new KeywordQueryRewriter();

        var rewritten = await rewriter.RewriteAsync(Fixtures.Query("vector index; then graph index fallback"));

        Assert.Equal("vector index graph fallback", rewritten);
    }

    [Fact]
    public async Task Null_query_throws()
    {
        var rewriter = new KeywordQueryRewriter();

        await Assert.ThrowsAsync<ArgumentNullException>(() => rewriter.RewriteAsync(null!));
    }

    [Fact]
    public async Task Single_letter_and_digit_tokens_are_dropped()
    {
        var rewriter = new KeywordQueryRewriter();

        var rewritten = await rewriter.RewriteAsync(Fixtures.Query("a 5 x 7 vectors"));

        Assert.Equal("vectors", rewritten);
    }
}