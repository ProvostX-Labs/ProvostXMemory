using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.VectorIndex;

namespace ProvostXMemory.Retrieval;

/// <summary>
/// Multi-representation retrieval: instead of scoring whole-record content, the query is
/// embedded once and compared against each representation's precomputed vector for the
/// requested modality. Representation vectors are indexed in the vector index under
/// "<c>repr|&lt;recordId&gt;|&lt;index&gt;</c>" ids (see <see cref="RepresentationId"/>);
/// hits map back to their parent record, per-record scores take the best-matching
/// representation, and per-modality provenance is attached. Scope comes from the query;
/// registry lookups are scope-exact.
/// </summary>
public sealed class MultiRepresentationRetriever : IMultiRepresentationRetriever
{
    /// <summary>Prefix of vector-index ids that point at record representations.</summary>
    public const string RepresentationIdPrefix = "repr|";

    private const string RepresentationIdSeparator = "|";

    private const string RetrieverName = "multi-representation";

    private readonly IScopeIndexRegistry _registry;
    private readonly IVectorEmbedder _embedder;

    /// <summary>
    /// Creates the retriever: the query is embedded once and compared against every
    /// representation vector; per record the best representation score wins
    /// (deterministic ties by record id).
    /// </summary>
    public MultiRepresentationRetriever(
        IScopeIndexRegistry registry,
        IVectorEmbedder embedder)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
    }

    /// <inheritdoc />
    public async Task<RetrievalResult> RetrieveAsync(
        RetrievalQuery query,
        MemoryRepresentationModality modality,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Scope);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.TopK);

        var scope = query.Scope;
        var index = _registry.GetVectorIndex(scope);
        var provider = _registry.GetRecordProvider(scope);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var queryVector = await _embedder.EmbedAsync(query.Text, cancellationToken).ConfigureAwait(false);
        if (queryVector.Length != index.Dim)
        {
            throw new InvalidOperationException(
                $"Embedder dim {queryVector.Length} does not match index dim {index.Dim} for scope '{scope.Kind}/{scope.Id}'.");
        }

        // Over-fetch: several representation hits can collapse into one parent record.
        var reprHits = await index
            .SearchAsync(queryVector, Math.Max(query.TopK * 4, 16), cancellationToken)
            .ConfigureAwait(false);
        var snapshot = await provider(scope, cancellationToken).ConfigureAwait(false);
        var byId = new Dictionary<string, MemoryRecord>(snapshot.Count, StringComparer.Ordinal);
        foreach (var record in snapshot)
        {
            byId[record.Id] = record;
        }

        var merged = MergeByRecord(modality, reprHits, byId);
        watch.Stop();

        var diagnostics = new List<RetrievalStageDiagnostics>
        {
            new(
                "multi-representation",
                watch.Elapsed,
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["representation-hits"] = reprHits.Count,
                    ["records"] = merged.Count,
                    ["modality"] = (double)modality,
                }),
        };

        return new RetrievalResult(merged.Take(query.TopK).ToList(), diagnostics);
    }

    private IReadOnlyList<ScoredMemoryRecord> MergeByRecord(
        MemoryRepresentationModality modality,
        IReadOnlyList<ScoredVectorId> reprHits,
        IReadOnlyDictionary<string, MemoryRecord> recordsById)
    {
        // Direct mode: best representation score per record, deterministic ties by record id.
        return reprHits
            .Select(hit => (Hit: hit, Parent: ParseParentId(hit.Id)))
            .Where(pair => pair.Parent is not null && recordsById.ContainsKey(pair.Parent!))
            .GroupBy(pair => pair.Parent!, StringComparer.Ordinal)
            .Select(group => new ScoredMemoryRecord(
                recordsById[group.Key],
                group.Max(pair => pair.Hit.Score),
                new RetrievalProvenance(
                    RetrieverName,
                    new Dictionary<string, double>(StringComparer.Ordinal)
                    {
                        [$"repr.{modality}"] = group.Max(pair => pair.Hit.Score),
                        ["best-representation"] = group.Max(pair => pair.Hit.Score),
                    })))
            .OrderByDescending(record => record.Score)
            .ThenBy(record => record.Record.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Parses "repr|&lt;recordId&gt;|&lt;index&gt;" to the parent record id; null when not a representation id.</summary>
    // Stateless utility (pure): parses the id string alone; no state.
    public static string? ParseParentId(string representationId)
    {
        ArgumentNullException.ThrowIfNull(representationId);
        if (!representationId.StartsWith(RepresentationIdPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = representationId[RepresentationIdPrefix.Length..];
        var lastSeparator = rest.LastIndexOf('|');
        return lastSeparator <= 0 ? null : rest[..lastSeparator];
    }

    /// <summary>Builds the canonical representation vector id ("repr|&lt;recordId&gt;|&lt;index&gt;").</summary>
    // Pure id-composition function; intentional static per architecture rule.
    public static string RepresentationId(string recordId, int representationIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentOutOfRangeException.ThrowIfNegative(representationIndex);

        // The pipe is the id separator: a recordId containing it produces an id
        // ParseParentId can never decode (silent hit loss). Reject loudly at compose time
        // (same discipline as ChunkIdCodec; 2026-10-14 final audit).
        if (recordId.Contains('|'))
        {
            throw new ArgumentException($"Record id must not contain '{RepresentationIdSeparator}'.", nameof(recordId));
        }

        return $"{RepresentationIdPrefix}{recordId}{RepresentationIdSeparator}{representationIndex}";
    }
}