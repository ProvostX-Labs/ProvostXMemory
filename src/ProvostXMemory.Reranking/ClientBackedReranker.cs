using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Reranking;

/// <summary>
/// Adapter wrapping the host-injected <see cref="IRerankerClient"/>: the client returns
/// best-first ids; this adapter maps ids back to candidates, keeps provenance of the
/// wrapped (fused) scores, and handles unlisted candidates per
/// <see cref="ClientBackedRerankerOptions"/>. The client itself stays host-owned infrastructure.
/// </summary>
public sealed class ClientBackedReranker : IReranker
{
    private const string RetrieverName = "rerank.client-backed";

    private readonly IRerankerClient _client;
    private readonly IReranker _fallback;
    private readonly ClientBackedRerankerOptions _options;

    /// <summary>
    /// Composes the adapter: <paramref name="client"/> supplies external ranking,
    /// <paramref name="fallback"/> (identity unless the host injects one) reranks whatever
    /// the client dropped when <see cref="ClientBackedRerankerOptions.TrustClientCompletely"/>
    /// is false.
    /// </summary>
    public ClientBackedReranker(
        IRerankerClient client,
        IReranker? fallback = null,
        ClientBackedRerankerOptions? options = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fallback = fallback ?? new NoopReranker();
        _options = options ?? new ClientBackedRerankerOptions();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScoredMemoryRecord>> RerankAsync(
        RetrievalQuery query,
        IReadOnlyList<ScoredMemoryRecord> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidates);
        cancellationToken.ThrowIfCancellationRequested();

        var candidateById = new Dictionary<string, ScoredMemoryRecord>(StringComparer.Ordinal);
        var originalRanks = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var rank = 0; rank < candidates.Count; rank++)
        {
            var candidate = candidates[rank];
            ArgumentNullException.ThrowIfNull(candidate);
            candidateById[candidate.Record.Id] = candidate;
            originalRanks[candidate.Record.Id] = rank;
        }

        var clientIds = await _client
            .RerankAsync(query.Text, candidates.Select(c => c.Record.Id).ToList(), cancellationToken)
            .ConfigureAwait(false);
        clientIds ??= Array.Empty<string>();

        var idSet = new HashSet<string>(StringComparer.Ordinal);
        var clientRanked = new List<ScoredMemoryRecord>();
        foreach (var id in clientIds)
        {
            // Duplicate ids collapse to first mention; unknown ids are ignored contractually.
            if (!candidateById.TryGetValue(id, out var candidate) || !idSet.Add(id))
            {
                continue;
            }

            clientRanked.Add(candidate);
        }

        if (_options.TrustClientCompletely)
        {
            return clientRanked;
        }

        var unlisted = candidates
            .Where(candidate => !idSet.Contains(candidate.Record.Id))
            .ToList();

        IReadOnlyList<ScoredMemoryRecord> orderedUnlisted = unlisted;
        if (unlisted.Count > 0)
        {
            orderedUnlisted = await _fallback.RerankAsync(query, unlisted, cancellationToken).ConfigureAwait(false);
        }

        return clientRanked.Concat(orderedUnlisted).ToList();
    }
}