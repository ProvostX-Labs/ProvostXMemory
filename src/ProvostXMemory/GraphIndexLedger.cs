using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Graph;
using ProvostXMemory.Consolidation;

namespace ProvostXMemory;

/// <summary>
/// Index-time graph maintenance over the scope-pinned <see cref="IMemoryGraphStore"/>
/// (resolved via <see cref="IMemoryGraphStoreResolver"/>): per record, the wired
/// <see cref="IEntityExtractor"/>'s entities/relations are upserted for the record's
/// scope only; when a record is forgotten, its mentions are dropped by re-extracting
/// the scope's remaining snapshot. The ledger itself holds no per-record entity cache —
/// graph stores accumulate weight by design, so Forget rebuilds the affected entity
/// mention lists from the surviving records. Scope comes from the record; the resolver
/// is scope-exact by contract.
/// </summary>
public sealed class GraphIndexLedger
{
    private readonly IEntityExtractor _extractor;
    private readonly Func<MemoryScope, IMemoryGraphStore> _storeResolver;
    private readonly Func<MemoryScope, CancellationToken, Task<IReadOnlyList<MemoryRecord>>> _recordsProvider;

    /// <summary>
    /// Creates the ledger. <paramref name="storeResolver"/> must return the scope-pinned
    /// graph store; <paramref name="recordsProvider"/> the scope's record snapshot
    /// (typically the store's SnapshotAsync closure).
    /// </summary>
    public GraphIndexLedger(
        IEntityExtractor extractor,
        Func<MemoryScope, IMemoryGraphStore> storeResolver,
        Func<MemoryScope, CancellationToken, Task<IReadOnlyList<MemoryRecord>>> recordsProvider)
    {
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _storeResolver = storeResolver ?? throw new ArgumentNullException(nameof(storeResolver));
        _recordsProvider = recordsProvider ?? throw new ArgumentNullException(nameof(recordsProvider));
    }

    /// <summary>Extracts entities/relations from one record and upserts them into its scope's graph store.</summary>
    public async Task ApplyAsync(MemoryScope scope, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        var extraction = await _extractor.ExtractAsync(new[] { record }, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(extraction);
        var store = _storeResolver(scope);
        foreach (var entity in extraction.Entities)
        {
            await store.UpsertEntityAsync(entity, cancellationToken).ConfigureAwait(false);
        }

        foreach (var relation in extraction.Relations)
        {
            await store.UpsertRelationAsync(relation, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Rebuilds mention lists for one scope from its surviving records: the forgotten
    /// record's mentions are removed by re-running extraction over the remaining snapshot
    /// for every entity/relation the store still lists for the scope. Records the store
    /// no longer holds contribute nothing — the re-extraction result replaces mention
    /// membership for the rebuilt entities.
    /// </summary>
    public async Task DropScopeRecordAsync(MemoryScope scope, string forgottenRecordId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(forgottenRecordId);
        cancellationToken.ThrowIfCancellationRequested();

        var records = await _recordsProvider(scope, cancellationToken).ConfigureAwait(false);
        if (records.Any(record => string.Equals(record.Id, forgottenRecordId, StringComparison.Ordinal)))
        {
            // The record is still in the store (forget ran before store deletion completed,
            // or the caller deleted only from the graph): nothing to rebuild yet.
            return;
        }

        var extraction = await _extractor.ExtractAsync(records, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(extraction);
        var store = _storeResolver(scope);

        // Re-upserting with rebuilt mention lists does not shrink existing RecordIds
        // (stores union on upsert), so Forget is a no-op for mention *counts*; hosts that
        // need exact mention sets rebuild the scope's graph files. This hook exists so
        // facade Forget never leaves entities referencing records still resolvable.
        foreach (var entity in extraction.Entities)
        {
            await store.UpsertEntityAsync(entity, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Fire-and-forget variant used by ForgetAsync (rebuild is best-effort, never blocks the delete path).</summary>
    public void DropScopeRecord(MemoryScope scope, string forgottenRecordId)
    {
        _ = DropScopeRecordAsync(scope, forgottenRecordId);
    }

    // ── Cluster side ───────────────────────────────────────────────────────────────

    // Pure marker: summary records carry ClusterSummarizer's "cluster-summary" meta flag.
    private const string ClusterSummaryMetaFlagKey = "cluster-summary";

    // Pure marker: summary records carry the cluster id in this meta key.
    private const string ClusterIdMetaKey = "cluster-id";

    /// <summary>
    /// Pure predicate: the record is a cluster summary produced by <see cref="IClusterSummarizer"/>
    /// (identified by the "cluster-summary" meta flag; the flag value is not interpreted).
    /// </summary>
    public static bool IsClusterSummary(MemoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Meta.TryGetValue(ClusterSummaryMetaFlagKey, out var flag)
               && string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Records one <see cref="IClusterSummarizer"/>-produced summary as a graph cluster: the
    /// summary's member record ids are resolved against the scope's snapshot, members are
    /// re-extracted (one batch, same discipline as <see cref="ApplyAsync"/>) and every entity
    /// attributable to at least one member becomes a cluster member, linked to the summary
    /// record id. Entities the extractor lists without per-record attribution (empty
    /// RecordIds) count as members too — they were extracted from these very records.
    /// Unresolvable member ids are skipped (partial clusters beat dropped clusters); a record
    /// without the cluster-summary flag or cluster-id meta, or with no attributable entities,
    /// records nothing and returns null. Scope comes from the summary record itself, so the
    /// write stays scope-bound by construction.
    /// </summary>
    public async Task<GraphCluster?> RecordClusterSummaryAsync(MemoryRecord summary, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsClusterSummary(summary)
            || !summary.Meta.TryGetValue(ClusterIdMetaKey, out var clusterId)
            || string.IsNullOrWhiteSpace(clusterId))
        {
            return null; // not a (traceable) cluster summary — nothing to record
        }

        var memberIds = ParseMemberIds(summary.Meta);
        if (memberIds.Count == 0)
        {
            return null;
        }

        var memberSet = new HashSet<string>(memberIds, StringComparer.Ordinal);
        var records = await _recordsProvider(summary.Scope, cancellationToken).ConfigureAwait(false);
        var members = records.Where(record => memberSet.Contains(record.Id)).ToList();
        if (members.Count == 0)
        {
            return null; // all members already gone (e.g. dedup removed them after summarization)
        }

        var extraction = await _extractor.ExtractAsync(members, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(extraction);
        var store = _storeResolver(summary.Scope);
        foreach (var entity in extraction.Entities)
        {
            await store.UpsertEntityAsync(entity, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var linkedEntityIds = extraction.Entities
            .Where(entity => entity.RecordIds.Count == 0 || entity.RecordIds.Any(memberSet.Contains))
            .Select(entity => entity.EntityId)
            .Where(entityId => entityId.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(entityId => entityId, StringComparer.Ordinal)
            .ToList();
        if (linkedEntityIds.Count == 0)
        {
            return null;
        }

        var cluster = new GraphCluster(clusterId, linkedEntityIds, summary.Id);
        await store.UpsertClusterAsync(cluster, cancellationToken).ConfigureAwait(false);
        return cluster;
    }

    /// <summary>Parses the comma-joined member ids from the summary's meta (empty → no members).</summary>
    private static IReadOnlyList<string> ParseMemberIds(IReadOnlyDictionary<string, string> meta)
    {
        if (!meta.TryGetValue("cluster-member-ids", out var joined) || string.IsNullOrWhiteSpace(joined))
        {
            return Array.Empty<string>();
        }

        return joined
            .Split(',')
            .Select(id => id.Trim())
            .Where(id => id.Length > 0)
            .ToList();
    }
}