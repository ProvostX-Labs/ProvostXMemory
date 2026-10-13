using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Correction;

/// <summary>Rewrites/expands a query for corrective retrieval (host-injected when an LLM-backed rewriter is desired).</summary>
public interface IQueryRewriter
{
    /// <summary>Returns the rewritten query text for <paramref name="query"/>.</summary>
    Task<string> RewriteAsync(RetrievalQuery query, CancellationToken cancellationToken = default);
}