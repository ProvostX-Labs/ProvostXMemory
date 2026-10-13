using System.Linq;

using ProvostXMemory.Abstractions;

using Xunit;

namespace ProvostXMemory.AbstractionsTests;

/// <summary>Shared token primitive behind rewriter + reranker: terms, stopword, acronym rules.</summary>
public sealed class QueryTokensTests
{
    [Fact]
    public void KeyTerms_drop_stopwords_and_keep_content_words()
    {
        Assert.Equal(
            new[] { "best", "way", "store", "data" },
            QueryTokens.KeyTerms("what is the best way to store data"));
    }

    [Fact]
    public void KeyTerms_preserve_acronyms_and_symbol_tokens()
    {
        Assert.Equal(new[] { "HTTP", "API", ".NET", "C#", "C++" }, QueryTokens.KeyTerms("the HTTP API for .NET C# C++"));
    }

    [Fact]
    public void KeyTerms_collapse_duplicates_keeping_first_casing()
    {
        Assert.Equal(new[] { "Cache", "Redis" }, QueryTokens.KeyTerms("Cache cache CACHE Redis"));
    }

    [Fact]
    public void KeyTerms_keep_first_appearance_order()
    {
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, QueryTokens.KeyTerms("alpha the beta and gamma"));
    }

    [Fact]
    public void Empty_or_null_text_yields_no_terms()
    {
        Assert.Empty(QueryTokens.KeyTerms(string.Empty));
        Assert.Empty(QueryTokens.KeyTerms("   "));
        Assert.Empty(QueryTokens.KeyTerms(null!));
    }

    [Fact]
    public void Acronyms_are_never_stopwords_even_if_listed()
    {
        Assert.False(QueryTokens.IsStopword("IT")); // uppercase acronym beats stopword list
        Assert.True(QueryTokens.IsStopword("it"));
        Assert.True(QueryTokens.IsStopword("the"));
        Assert.False(QueryTokens.IsStopword("API"));
    }

    [Fact]
    public void IsAcronym_requires_uppercase_letters_allowing_symbols()
    {
        Assert.True(QueryTokens.IsAcronym("API"));
        Assert.True(QueryTokens.IsAcronym(".NET"));
        Assert.True(QueryTokens.IsAcronym("C#"));
        Assert.True(QueryTokens.IsAcronym("C++"));
        Assert.False(QueryTokens.IsAcronym("Api"));
        Assert.False(QueryTokens.IsAcronym("a"));
        Assert.False(QueryTokens.IsAcronym("42"));
    }
}