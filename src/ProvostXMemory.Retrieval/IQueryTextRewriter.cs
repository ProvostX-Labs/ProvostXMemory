using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Retrieval;

/// <summary>
/// Rewrites a query's plain text before a second retrieval pass (host-injected;
/// typically an LLM-backed rewriter, a lexical distiller, or a spell corrector).
/// Lives in Retrieval (not Correction) so plan fan-out can iterate without the
/// Correction package as a dependency.
/// </summary>
public interface IQueryTextRewriter
{
    /// <summary>Returns the rewritten text for <paramref name="text"/>.</summary>
    Task<string> RewriteAsync(string text, CancellationToken cancellationToken = default);
}