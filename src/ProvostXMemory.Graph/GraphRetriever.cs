using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Graph;

/// <summary>
/// Graph-backed retriever: lowercased query tokens are matched against entity ids and
/// entity names/aliases (via <see cref="IMemoryGraphStore.SearchEntitiesAsync"/>) and against
/// record tags from the scope's snapshot; matched entities seed a multi-source neighbor walk up
/// to <see cref="GraphOptions.MaxHops"/> hops via <see cref="IMemoryGraphStore.NeighborsAsync"/>;
/// records touched by discovered entities and their relations plus
/// <see cref="GraphCluster.SummaryRecordId"/> of matched clusters are scored by hop distance
/// (direct evidence = 1.0, each additional hop × 0.5 decay; independent evidence sums).
/// Entity/relation extraction is an index-time concern (<see cref="IEntityExtractor"/>);
/// this retriever is deliberately query-time token-based.
/// </summary>
public sealed class GraphRetriever : IGraphRetriever
{
    private const string RetrieverName = "graph"; // compile-time constant; no shared mutable state
    private const double DirectEvidenceWeight = 1.0;
    private const double RelationEvidenceWeight = 0.8;
    private const double ClusterSummaryWeight = 0.9;
    private const double HopDecayFactor = 0.5;

    private readonly Func<MemoryScope, IMemoryGraphStore> _storeFactory;
    private readonly Func<MemoryScope, CancellationToken, Task<IReadOnlyList<MemoryRecord>>> _recordsProvider;
    private readonly GraphOptions _options;

    /// <summary>
    /// Creates the retriever. <paramref name="storeFactory"/> must return the scope-pinned graph
    /// store for the requested scope and <paramref name="recordsProvider"/> the scope's memory
    /// snapshot (structural scope isolation; typically wired to the scope store's Snapshot).
    /// </summary>
    public GraphRetriever(
        Func<MemoryScope, IMemoryGraphStore> storeFactory,
        Func<MemoryScope, CancellationToken, Task<IReadOnlyList<MemoryRecord>>> recordsProvider,
        GraphOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(storeFactory);
        ArgumentNullException.ThrowIfNull(recordsProvider);
        _storeFactory = storeFactory;
        _recordsProvider = recordsProvider;
        _options = options ?? GraphOptions.Default; // pure fallback factory; no shared mutable state
    }

    public async Task<IReadOnlyList<ScoredMemoryRecord>> RetrieveAsync(string query, MemoryScope scope, int topK, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();
        if (topK <= 0 || string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ScoredMemoryRecord>(); // pure empty factory
        }

        var store = _storeFactory(scope);
        var records = await _recordsProvider(scope, cancellationToken).ConfigureAwait(false);

        var evidence = new Dictionary<string, double>(StringComparer.Ordinal);
        var matchedEntityIds = new List<string>();
        var seedEntityIds = new HashSet<string>(StringComparer.Ordinal);
        var queryTokens = QueryTokens(query);

        // Seed 1: exact token → entity id ("alice-smith" matches the entity id directly).
        foreach (var token in queryTokens)
        {
            var entity = await TryGetEntitySafeAsync(store, token, cancellationToken).ConfigureAwait(false);
            if (entity is not null && seedEntityIds.Add(entity.EntityId))
            {
                matchedEntityIds.Add(entity.EntityId);
            }
        }

        // Seed 2: token-overlap search over entity names/aliases.
        foreach (var entity in await SearchEntitiesSafeAsync(store, query, cancellationToken).ConfigureAwait(false))
        {
            if (seedEntityIds.Add(entity.EntityId))
            {
                matchedEntityIds.Add(entity.EntityId);
            }
        }

        // Seed 3: records whose tags contain a query token match as hop-0 evidence.
        foreach (var record in records)
        {
            if (MatchesByTag(record, queryTokens))
            {
                Accumulate(evidence, record.Id, DirectEvidenceWeight);
            }
        }

        if (matchedEntityIds.Count > 0)
        {
            await WalkAsync(store, matchedEntityIds, evidence, cancellationToken).ConfigureAwait(false);
        }

        if (evidence.Count == 0)
        {
            return Array.Empty<ScoredMemoryRecord>();
        }

        var byId = new Dictionary<string, MemoryRecord>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            byId[record.Id] = record;
        }

        return evidence
            .Where(kv => byId.ContainsKey(kv.Key))
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(topK)
            .Select(kv => new ScoredMemoryRecord(
                byId[kv.Key],
                kv.Value,
                new RetrievalProvenance(
                    RetrieverName,
                    new Dictionary<string, double>(StringComparer.Ordinal)
                    {
                        ["graph-score"] = kv.Value,
                    })))
            .ToList();
    }

    /// <summary>
    /// Multi-source BFS: hop-0 seeds score their own mentions at full weight; each discovered
    /// neighbor entity scores its mentions one hop deeper; relations seen during the walk
    /// contribute their supporting records at the endpoint's hop distance.
    /// </summary>
    private async Task WalkAsync(IMemoryGraphStore store, IReadOnlyList<string> seedEntityIds, Dictionary<string, double> evidence, CancellationToken cancellationToken)
    {
        var hops = new Dictionary<string, int>(StringComparer.Ordinal);
        var frontier = new List<string>();
        foreach (var entityId in seedEntityIds)
        {
            if (hops.TryAdd(entityId, 0))
            {
                frontier.Add(entityId);
            }
        }

        var maxHops = Math.Max(0, _options.MaxHops);
        var creditedRelations = new HashSet<string>(StringComparer.Ordinal);
        for (var hop = 0; hop <= maxHops && frontier.Count > 0; hop++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entityScore = DirectEvidenceWeight * Math.Pow(HopDecayFactor, hop);
            var relationScore = RelationEvidenceWeight * Math.Pow(HopDecayFactor, hop + 1);
            var nextFrontier = new List<string>();

            foreach (var entityId in frontier)
            {
                var entity = await TryGetEntitySafeAsync(store, entityId, cancellationToken).ConfigureAwait(false);
                if (entity is not null)
                {
                    foreach (var recordId in entity.RecordIds)
                    {
                        Accumulate(evidence, recordId, entityScore);
                    }
                }

                var relations = await RelationsOfSafeAsync(store, entityId, cancellationToken).ConfigureAwait(false);
                foreach (var relation in relations)
                {
                    // Credit each relation once, at the earliest hop it is discovered, so the
                    // same relation is never re-credited from its other endpoint deeper in the walk.
                    if (!creditedRelations.Add(relation.RelationId))
                    {
                        continue;
                    }

                    foreach (var recordId in relation.RecordIds)
                    {
                        Accumulate(evidence, recordId, relationScore);
                    }
                }

                if (hop >= maxHops)
                {
                    continue;
                }

                var neighbors = await store.NeighborsAsync(entityId, _options.MaxNeighbors, cancellationToken).ConfigureAwait(false);
                foreach (var neighbor in neighbors)
                {
                    if (hops.TryAdd(neighbor.EntityId, hop + 1))
                    {
                        nextFrontier.Add(neighbor.EntityId);
                    }
                }
            }

            frontier = nextFrontier;
        }

        // Cluster summaries of any discovered entity join the result with their own weight.
        foreach (var entityId in hops.Keys)
        {
            var cluster = await ClusterOfSafeAsync(store, entityId, cancellationToken).ConfigureAwait(false);
            if (cluster?.SummaryRecordId is { } summaryId)
            {
                var hop = hops[entityId];
                Accumulate(evidence, summaryId, ClusterSummaryWeight * Math.Pow(HopDecayFactor, Math.Min(hop, maxHops)));
            }
        }
    }

    // Pure predicate: any query token appears among the record's tag tokens; no shared mutable state.
    private static bool MatchesByTag(MemoryRecord record, HashSet<string> queryTokens)
    {
        foreach (var tag in record.Tags)
        {
            var tagLower = tag.ToLowerInvariant();
            if (queryTokens.Contains(tagLower))
            {
                return true;
            }

            foreach (var token in Tokenize(tag))
            {
                if (queryTokens.Contains(token))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static async Task<GraphEntity?> TryGetEntitySafeAsync(IMemoryGraphStore store, string entityId, CancellationToken cancellationToken)
    {
        try
        {
            return await store.GetEntityAsync(entityId, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            return null; // tokens that are not valid entity ids simply do not match
        }
    }

    private static async Task<IReadOnlyList<GraphEntity>> SearchEntitiesSafeAsync(IMemoryGraphStore store, string query, CancellationToken cancellationToken)
    {
        try
        {
            return await store.SearchEntitiesAsync(query, 10, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            return Array.Empty<GraphEntity>(); // pure empty factory
        }
    }

    private static async Task<IReadOnlyList<GraphRelation>> RelationsOfSafeAsync(IMemoryGraphStore store, string entityId, CancellationToken cancellationToken)
    {
        try
        {
            return await store.RelationsOfAsync(entityId, 32, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            return Array.Empty<GraphRelation>(); // pure empty factory
        }
    }

    private static async Task<GraphCluster?> ClusterOfSafeAsync(IMemoryGraphStore store, string entityId, CancellationToken cancellationToken)
    {
        try
        {
            return await store.ClusterOfAsync(entityId, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static void Accumulate(Dictionary<string, double> evidence, string recordId, double score)
    {
        evidence[recordId] = evidence.TryGetValue(recordId, out var current) ? current + score : score;
    }

    // Pure lowercase-alphanumeric token set factory (min length 2); no shared mutable state.
    private static HashSet<string> QueryTokens(string query)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in Tokenize(query))
        {
            if (token.Length >= 2)
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    // Pure token factory; no shared mutable state.
    private static IEnumerable<string> Tokenize(string text)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (builder.Length > 0)
            {
                yield return builder.ToString();
                builder.Clear();
            }
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }
}