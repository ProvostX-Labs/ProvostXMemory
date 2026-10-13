using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Correction;

/// <summary>
/// Fetches content from external sources (host-injected; the library stays network-agnostic).
/// Returning an empty list (or null) yields no fetched-stage content.
/// </summary>
public interface IExternalFetcher
{
    Task<IReadOnlyList<string>?> FetchAsync(RetrievalQuery query, CancellationToken cancellationToken = default);
}