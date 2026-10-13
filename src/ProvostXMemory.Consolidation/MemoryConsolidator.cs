using ProvostXMemory.Abstractions;
using ProvostXMemory.VectorIndex;

namespace ProvostXMemory.Consolidation;

/// <summary>
/// One consolidation pass over a scope, executed against the scope-bound
/// <see cref="IMemoryStore"/>: (1) TTL expiry deletes records whose ExpiresAt is in the
/// past, (2) near-duplicate detection via <see cref="IDedupDetector"/> keeps the oldest
/// record of each duplicate group and unions the tags of the removed duplicates into the
/// kept record, (3) remaining records are clustered by embedding similarity and clusters
/// of sufficient size are emitted as summary records via <see cref="IClusterSummarizer"/>,
/// (4) all mutations are counted in a <see cref="ConsolidationReport"/> attached to the
/// returned records. Survivors and summaries are returned (survivors first, summaries
/// last); an empty scope is a no-op returning an empty list. Every deletion goes through
/// the store's scope-bound API — cross-scope access stays structurally impossible.
/// </summary>
public sealed class MemoryConsolidator : IMemoryConsolidator
{
    private readonly IMemoryStore _store;
    private readonly IDedupDetector _dedupDetector;
    private readonly IVectorEmbedder _embedder;
    private readonly IClusterSummarizer _clusterSummarizer;
    private readonly ConsolidationTuning _tuning;

    public MemoryConsolidator(
        IMemoryStore store,
        IDedupDetector dedupDetector,
        IVectorEmbedder embedder,
        IClusterSummarizer clusterSummarizer,
        ConsolidationTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(dedupDetector);
        ArgumentNullException.ThrowIfNull(embedder);
        ArgumentNullException.ThrowIfNull(clusterSummarizer);
        _store = store;
        _dedupDetector = dedupDetector;
        _embedder = embedder;
        _clusterSummarizer = clusterSummarizer;
        _tuning = tuning ?? new ConsolidationTuning();
    }

    public async Task<IReadOnlyList<MemoryRecord>> ConsolidateAsync(string scopeKind, string scopeId, ConsolidationOptions options, CancellationToken cancellationToken = default)
    {
        ValidateScopeParameters(scopeKind, scopeId);
        ArgumentNullException.ThrowIfNull(options);

        var records = await _store.ListAsync(scopeKind, scopeId, cancellationToken).ConfigureAwait(false);
        var working = records.ToList();
        var expiredDeleted = 0;
        var duplicatesDeleted = 0;

        // Stage 1 — TTL expiry: anything with a past ExpiresAt is deleted outright.
        if (options.ExpireTtl)
        {
            var now = DateTimeOffset.UtcNow;
            var survivors = new List<MemoryRecord>(working.Count);
            foreach (var record in working)
            {
                if (record.ExpiresAt is { } expiresAt && expiresAt <= now)
                {
                    await _store.DeleteAsync(scopeKind, scopeId, record.Id, cancellationToken).ConfigureAwait(false);
                    expiredDeleted++;
                }
                else
                {
                    survivors.Add(record);
                }
            }

            working = survivors;
        }

        // Stage 2 — dedup: keep the oldest member of each duplicate group, union tags.
        // Seeds are processed oldest-first (CreatedAt, then id) so the kept record of a
        // group is always the oldest; scan order over candidates stays list order.
        var kept = new List<MemoryRecord>(working.Count);
        var assigned = new HashSet<int>();
        var seedOrder = Enumerable.Range(0, working.Count)
            .OrderBy(index => working[index].CreatedAt)
            .ThenBy(index => working[index].Id, StringComparer.Ordinal)
            .ToList();
        foreach (var seedIndex in seedOrder)
        {
            if (assigned.Contains(seedIndex))
            {
                continue;
            }

            assigned.Add(seedIndex);
            var keptRecord = working[seedIndex];
            var keptTags = new HashSet<string>(keptRecord.Tags, StringComparer.Ordinal);
            for (var j = 0; j < working.Count; j++)
            {
                if (j == seedIndex || assigned.Contains(j))
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!await _dedupDetector
                    .IsDuplicateAsync(keptRecord, working[j], options.DedupSimilarityThreshold, cancellationToken)
                    .ConfigureAwait(false))
                {
                    continue;
                }

                assigned.Add(j);
                duplicatesDeleted++;
                keptTags.UnionWith(working[j].Tags);
                await _store.DeleteAsync(scopeKind, scopeId, working[j].Id, cancellationToken).ConfigureAwait(false);
            }

            if (keptTags.Count > keptRecord.Tags.Count)
            {
                keptRecord = keptRecord with { Tags = keptTags.OrderBy(tag => tag, StringComparer.Ordinal).ToList() };
                await _store.UpdateAsync(keptRecord, cancellationToken).ConfigureAwait(false);
            }

            kept.Add(keptRecord);
        }

        // Stage 3 — near-duplicate clusters → summary records.
        var summaries = new List<MemoryRecord>();
        if (options.EmitClusterSummaries)
        {
            var clusters = await BuildClustersAsync(kept, cancellationToken).ConfigureAwait(false);
            foreach (var cluster in clusters)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (cluster.Count < _tuning.SummarizeMinClusterSize)
                {
                    continue;
                }

                var summary = await _clusterSummarizer
                    .SummarizeAsync(scopeKind, scopeId, cluster, cancellationToken)
                    .ConfigureAwait(false);
                await _store.AddAsync(summary, cancellationToken).ConfigureAwait(false);
                summaries.Add(summary);
            }

            summaries.Sort(static (a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        return BuildResult(kept, summaries, expiredDeleted, duplicatesDeleted);
    }

    /// <summary>
    /// Clusters kept records by the injected embedder's vectors: greedy union of pairs
    /// whose cosine is at or above the cluster join threshold, first-record wins
    /// (deterministic input order). Zero-norm vectors fall back to lexical Jaccard.
    /// </summary>
    private async Task<List<List<MemoryRecord>>> BuildClustersAsync(
        IReadOnlyList<MemoryRecord> records,
        CancellationToken cancellationToken)
    {
        var clusters = new List<List<MemoryRecord>>();
        var vectors = new List<float[]>(records.Count);
        foreach (var record in records)
        {
            vectors.Add(await _embedder.EmbedAsync(record.Content, cancellationToken).ConfigureAwait(false));
        }

        var scalarOps = new ScalarVectorOps();
        var assigned = new HashSet<int>();
        for (var i = 0; i < records.Count; i++)
        {
            if (assigned.Contains(i))
            {
                continue;
            }

            assigned.Add(i);
            var cluster = new List<MemoryRecord> { records[i] };
            for (var j = i + 1; j < records.Count; j++)
            {
                if (assigned.Contains(j))
                {
                    continue;
                }

                var similarity = scalarOps.Cosine(vectors[i], vectors[j]);
                if (similarity <= float.Epsilon)
                {
                    similarity = (float)LexicalSimilarity(records[i].Content, records[j].Content);
                }

                if (similarity >= _tuning.ClusterJoinThreshold)
                {
                    assigned.Add(j);
                    cluster.Add(records[j]);
                }
            }

            // Deterministic cluster membership order (ordinal id) for stable summaries.
            cluster.Sort(static (a, b) => string.CompareOrdinal(a.Id, b.Id));
            clusters.Add(cluster);
        }

        return clusters;
    }

    private static IReadOnlyList<MemoryRecord> BuildResult(
        List<MemoryRecord> kept,
        List<MemoryRecord> summaries,
        int expiredDeleted,
        int duplicatesDeleted)
    {
        var orderedKept = kept.OrderBy(record => record.Id, StringComparer.Ordinal).ToList();
        var merged = new List<MemoryRecord>(orderedKept.Count + summaries.Count);
        merged.AddRange(orderedKept);
        merged.AddRange(summaries);

        var report = new ConsolidationReport
        {
            RecordsConsidered = orderedKept.Count + expiredDeleted + duplicatesDeleted,
            ExpiredDeleted = expiredDeleted,
            DuplicatesDeleted = duplicatesDeleted,
            SummariesEmitted = summaries.Count,
            RecordsRemaining = orderedKept.Count + summaries.Count,
        };

        return ConsolidationReportMeta.Apply(merged, report);
    }

    private static void ValidateScopeParameters(string scopeKind, string scopeId)
    {
        if (string.IsNullOrWhiteSpace(scopeKind))
        {
            throw new ArgumentException("Scope kind is required.", nameof(scopeKind));
        }

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            throw new ArgumentException("Scope id is required.", nameof(scopeId));
        }
    }

    /// <summary>Lexical Jaccard over lower-cased letter/digit token sets (fallback when vectors degenerate).</summary>
    private static double LexicalSimilarity(string first, string second)
    {
        var firstTokens = TokenSet(first);
        var secondTokens = TokenSet(second);
        if (firstTokens.Count == 0 && secondTokens.Count == 0)
        {
            return 1.0;
        }

        var intersection = 0;
        if (firstTokens.Overlaps(secondTokens))
        {
            foreach (var token in secondTokens)
            {
                if (firstTokens.Contains(token))
                {
                    intersection++;
                }
            }
        }

        var union = firstTokens.Count + secondTokens.Count - intersection;
        return union == 0 ? 1.0 : (double)intersection / union;
    }

    private static HashSet<string> TokenSet(string text)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var isWord = i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '\'');
            if (isWord && start < 0)
            {
                start = i;
            }
            else if (!isWord && start >= 0)
            {
                tokens.Add(text[start..i].ToLowerInvariant());
                start = -1;
            }
        }

        return tokens;
    }
}