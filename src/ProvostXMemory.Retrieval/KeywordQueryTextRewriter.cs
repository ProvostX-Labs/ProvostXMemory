using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Lexical;

namespace ProvostXMemory.Retrieval;

/// <summary>
/// Zero-dependency <see cref="IQueryTextRewriter"/>: distills the query text to its
/// key terms via <see cref="QueryTokens.KeyTerms"/> — stopwords stripped (acronyms
/// preserved), duplicates collapsed, first-appearance order. Empty queries stay empty.
/// </summary>
public sealed class KeywordQueryTextRewriter : IQueryTextRewriter
{
    /// <inheritdoc />
    public Task<string> RewriteAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        var terms = QueryTokens.KeyTerms(text);
        return Task.FromResult(string.Join(' ', terms));
    }
}