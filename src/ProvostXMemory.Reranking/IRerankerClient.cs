using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Reranking;

/// <summary>
/// Optional external rerank service contract (LLM/cross-encoder API, host-injected).
/// The library never constructs a client; hosts adapt their transport into this contract.
/// </summary>
public interface IRerankerClient
{
    /// <summary>
    /// Reranks the candidate ids against the query and returns them best-first.
    /// Implementations may return a subset (dropped candidates fall back per the wrapping
    /// reranker's options) but never ids outside the input.
    /// </summary>
    Task<IReadOnlyList<string>> RerankAsync(string query, IReadOnlyList<string> candidateIds, CancellationToken cancellationToken = default);
}