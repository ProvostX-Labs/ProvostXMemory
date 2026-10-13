using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory;

/// <summary>
/// <para>The single public surface of the ProvostXMemory library. Host applications see
/// this interface — plus the options records they pass to <see cref="MemoryServiceBuilder"/> —
/// and nothing else. Every capability of the library (durable storage, hybrid lexical and
/// dense-vector retrieval, fusion, reranking, optional corrective and graph retrieval,
/// consolidation, and audit-grade logging) is reachable through the members below.</para>
/// <para><b>Scope isolation.</b> Every record belongs to exactly one <see cref="MemoryScope"/>
/// (platform, session, tenant, purpose, or custom). Every operation is scope-bound; a record
/// written to scope A is structurally invisible to operations on scope B — the storage layout
/// and the index registry make cross-scope access impossible rather than merely discouraged.</para>
/// <para><b>Concurrency.</b> Implementations are safe for concurrent use from multiple threads.
/// Mutations (Remember/Update/Forget) are serialized per service instance; embedding computation
/// happens outside the serialization gate, so concurrent callers do not block on model latency.</para>
/// </summary>
/// <remarks>
/// <para>A <see cref="MemoryService"/> is composed via <see cref="MemoryServiceBuilder"/>:
/// construct the service by wiring the required dependencies (store, embedder, index factory,
/// fuser) and any optional capabilities (grader → corrective loop, graph extractor, reranker,
/// chunker, log sink), then hold the returned <c>IMemoryService</c> for the lifetime of your host.</para>
/// <para>Retrieval quality is observable through <see cref="RetrievalResult"/> diagnostics:
/// every <see cref="RecallAsync"/> returns per-stage scores and timings, which the
/// <c>ProvostXMemory.Eval</c> harness turns into Recall@k / Precision@k / MRR / NDCG reports.</para>
/// </remarks>
/// <example>
/// <code>
/// var service = new MemoryServiceBuilder()
///     .WithOptions(options)
///     .WithStore(store)
///     .WithEmbedder(embedder)
///     .WithIndexFactory(new EngineVectorIndexFactory())
///     .WithFuser(new ReciprocalRankFuser())
///     .WithTelemetry(NoopMemoryTelemetry.Instance)
///     .Build();
///
/// var scope = new MemoryScope(MemoryScopeKind.Session, "user-42");
/// var fact = new MemoryRecord(
///     Id: "fact-1",
///     Scope: scope,
///     Key: "api gateway",
///     Content: "The API gateway binds on port 48321.");
/// await service.RememberAsync(fact);
/// var result = await service.RecallAsync(new RetrievalQuery("which port", scope, 5));
/// </code>
/// </example>
public interface IMemoryService
{
    /// <summary>
    /// Persists a record and indexes it for retrieval: the record is written to the scope's
    /// durable store, then chunks, whole-record, and per-representation entries are embedded
    /// and added to the scope's vector index, and the full text is added to the scope's
    /// lexical index. A record id that already exists in the scope is rejected.
    /// </summary>
    /// <exception cref="System.ArgumentNullException"><paramref name="record"/> is null.</exception>
    /// <exception cref="System.InvalidOperationException">A record with the same id already exists in the scope.</exception>
    Task RememberAsync(MemoryRecord record, CancellationToken cancellationToken = default);

    /// <summary>Removes a record from the scope's store and every index (vector, lexical, graph).</summary>
    /// <remarks>Idempotent: forgetting an unknown id completes without error.</remarks>
    Task ForgetAsync(MemoryScope scope, string id, CancellationToken cancellationToken = default);

    /// <summary>Fetches one record by id from the given scope; null when unknown.</summary>
    Task<MemoryRecord?> GetAsync(MemoryScope scope, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a record in place (matched by id within the record's scope) and refreshes
    /// all index entries (lexical, vector, graph) for the new content.
    /// </summary>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">No record with the given id exists in the scope.</exception>
    Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Routes the query through the configured retrieval pipeline: hybrid dense+lexical
    /// search fused with Reciprocal Rank Fusion, optionally reranked; when a grader is wired,
    /// a corrective loop grades candidates, rewrites weak queries, and retries once. Scope
    /// comes from the query; results never cross scopes.
    /// </summary>
    /// <remarks>
    /// An empty or whitespace query text returns an empty result (diagnostics carry the
    /// <c>empty-query</c> stage) rather than performing an unconstrained nearest-neighbor search.
    /// </remarks>
    Task<RetrievalResult> RecallAsync(RetrievalQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs one consolidation pass over a scope: TTL expiry deletes records whose
    /// <c>ExpiresAt</c> is in the past, near-duplicates are detected (embeddings with a
    /// lexical fallback), the oldest record of each duplicate group is kept with the
    /// group's tags unioned in, and sufficiently large near-duplicate clusters emit a
    /// summary record. Survivors and summaries are returned as the consolidated set.
    /// </summary>
    Task ConsolidateAsync(MemoryScope scope, ConsolidationOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the scope's indexes from the store's current snapshot (retrieval works without
    /// this when records were remembered through this service; call it after populating a
    /// store directly, or after restart).
    /// </summary>
    Task BuildIndexesAsync(MemoryScope scope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds every index of every scope from the stores' current snapshots; index state
    /// derived from removed or stale records is discarded.
    /// </summary>
    Task RebuildAsync(CancellationToken cancellationToken = default);

    /// <summary>Enumerates every scope that exists under the service's storage root.</summary>
    /// <returns>Scopes in deterministic order (kind, then id).</returns>
    IReadOnlyList<MemoryScope> Scopes();
}