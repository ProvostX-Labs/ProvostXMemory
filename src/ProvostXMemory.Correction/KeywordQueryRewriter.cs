using System;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Retrieval;

namespace ProvostXMemory.Correction;

/// <summary>
/// Deterministic, zero-dependency query rewriter: distills the query to its key terms via
/// <see cref="QueryTokens.KeyTerms(string)"/> — stopwords stripped (acronyms preserved),
/// duplicates collapsed. Emits a single-space-joined term list; empty queries rewrite to
/// the empty string.
/// </summary>
public sealed class KeywordQueryRewriter : IQueryRewriter
{
    /// <inheritdoc />
    public Task<string> RewriteAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var terms = QueryTokens.KeyTerms(query.Text);
        return Task.FromResult(string.Join(' ', terms));
    }
}