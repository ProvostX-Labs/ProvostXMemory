using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.Lexical;

using Xunit;

namespace ProvostXMemory.LexicalTests;

/// <summary>Tokenizer contract: lowercase [a-z0-9]+ word tokens (letters, digits, underscore).</summary>
public sealed class LexicalTokenizerTests
{
    private static IReadOnlyList<string> Tokenize(string text)
    {
        return LexicalTokenizer.Tokenize(text);
    }

    [Fact]
    public void Lowercases_and_splits_on_non_word_characters()
    {
        var tokens = Tokenize("Hello, World! FOSS-bird_32");
        Assert.Equal(new[] { "hello", "world", "foss", "bird_32" }, tokens);
    }

    [Fact]
    public void Digits_and_mixed_alphanumerics_are_kept()
    {
        var tokens = Tokenize("v2 x9y 003 42");
        Assert.Equal(new[] { "v2", "x9y", "003", "42" }, tokens);
    }

    [Fact]
    public void Underscore_is_a_word_character()
    {
        var tokens = Tokenize("snake_case case_snake _leading");
        Assert.Equal(new[] { "snake_case", "case_snake", "_leading" }, tokens);
    }

    [Fact]
    public void Unicode_letters_tokenize_and_fold()
    {
        var tokens = Tokenize("Straße ÄÖÜ");
        Assert.Equal(new[] { "straße", "äöü" }, tokens);
    }

    [Fact]
    public void Empty_and_whitespace_inputs_yield_no_tokens()
    {
        Assert.Empty(Tokenize(string.Empty));
        Assert.Empty(Tokenize("   \t\r\n  "));
        Assert.Empty(Tokenize("!!!...???"));
    }

    [Fact]
    public void Null_input_yields_no_tokens()
    {
        Assert.Empty(LexicalTokenizer.Tokenize(null));
    }

    [Fact]
    public void CountTokens_counts_occurrences_per_distinct_token()
    {
        var counts = LexicalTokenizer.CountTokens("Cat cat DOG, cat.");
        Assert.Equal(3, counts["cat"]);
        Assert.Equal(1, counts["dog"]);
        Assert.Equal(2, counts.Count);
    }

    [Fact]
    public void CountTokens_on_empty_input_returns_empty_counts()
    {
        Assert.Empty(LexicalTokenizer.CountTokens(string.Empty));
        Assert.Empty(LexicalTokenizer.CountTokens("!!!"));
        Assert.Empty(LexicalTokenizer.CountTokens(null));
    }
}