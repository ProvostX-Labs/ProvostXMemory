using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ProvostXMemory.Abstractions;

/// <summary>
/// Pure query-text token helpers shared by text-processing packages (keyword rewriting,
/// score-boost reranking). Stateless lookups and a compiled regex only; this class is the
/// architecture-sanctioned pure-function static: no I/O, no ambient state, deterministic.
/// </summary>
public static class QueryTokens
{
    // Acronym/symbol-aware token pattern: ".NET", "C++", "C#", "example.com" stay single
    // tokens; other punctuation splits. '+'/'#' may dangle at token end (C#, F#); '.'
    // requires a following word char so sentence-ending periods fall off the token.
    // Compiled regex is pure (stateless match).
    private static readonly Regex TokenPattern = new(
        @"(?:\.\w|\w)[\w]*(?:\+\+|[+#]\w*|\.\w+)*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(200));

    // Minimal function-word set for keyword queries; acronyms are exempt (IsAcronym).
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "if", "then", "when", "while",
        "of", "to", "in", "on", "at", "by", "for", "with", "about", "as",
        "into", "from", "is", "are", "was", "were", "be", "been", "being",
        "it", "its", "this", "that", "these", "those",
        "what", "which", "who", "how", "why", "where",
        "do", "does", "did", "doing", "have", "has", "had",
        "not", "no", "nor", "so", "too", "very",
        "can", "will", "would", "should", "could", "shall", "may", "might", "must",
        "now", "just", "also", "only", "than", "over", "under", "again", "here", "there",
        "i", "me", "my", "we", "our", "us", "you", "your",
        "he", "him", "his", "she", "her", "they", "them", "their",
    };

    /// <summary>
    /// True when <paramref name="token"/> reads like an acronym: at least one letter, all
    /// letters uppercase; digits and symbol characters ('.', '#', '+', '_') allowed.
    /// Examples: "API", "HTTP", ".NET", "C++", "C#", "F#".
    /// </summary>
    public static bool IsAcronym(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length < 2)
        {
            return false;
        }

        var hasLetter = false;
        foreach (var ch in token)
        {
            if (char.IsLetter(ch))
            {
                hasLetter = true;
                if (!char.IsUpper(ch))
                {
                    return false;
                }
            }
            else if (!char.IsDigit(ch) && ch != '_' && ch != '.' && ch != '#' && ch != '+')
            {
                return false;
            }
        }

        return hasLetter;
    }

    /// <summary>True for function words dropped from keyword queries; acronyms are never stopwords.</summary>
    public static bool IsStopword(string token)
        => !string.IsNullOrEmpty(token) && !IsAcronym(token) && Stopwords.Contains(token);

    /// <summary>
    /// Extracts the deterministic key terms of a query text: stopwords are dropped (acronyms
    /// exempt), standalone lowercase letters and digits are dropped, duplicate terms are
    /// collapsed case-insensitively keeping the first casing. Order of first appearance;
    /// casing of kept terms is preserved.
    /// </summary>
    public static IReadOnlyList<string> KeyTerms(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var terms = new List<string>();
        foreach (Match match in TokenPattern.Matches(text))
        {
            var token = match.Value;
            if (IsStopword(token))
            {
                continue;
            }

            if (token.Length == 1 && (char.IsLower(token[0]) || char.IsDigit(token[0])))
            {
                continue;
            }

            if (seen.Add(token))
            {
                terms.Add(token);
            }
        }

        return terms;
    }
}