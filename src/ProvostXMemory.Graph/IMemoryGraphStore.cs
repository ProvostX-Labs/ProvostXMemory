using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProvostXMemory.Graph;

/// <summary>Graph-backed store over memory records: entities, relations, paths, clusters.</summary>
public interface IMemoryGraphStore
{
    Task UpsertEntityAsync(GraphEntity entity, CancellationToken cancellationToken = default);

    Task UpsertRelationAsync(GraphRelation relation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or replaces the cluster: when a cluster with the same id exists, its member
    /// list and summary record id are replaced (clusters are the consolidation pass's
    /// authoritative output, not an accumulating union). Entities not in the new member list
    /// simply stop resolving to the cluster via <see cref="ClusterOfAsync"/>.
    /// </summary>
    Task UpsertClusterAsync(GraphCluster cluster, CancellationToken cancellationToken = default);

    /// <summary>Returns the entity with the given id, or null when unknown.</summary>
    Task<GraphEntity?> GetEntityAsync(string entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns entities whose name or any alias shares a whole token with the query
    /// (case-insensitive), strongest token overlap first.
    /// </summary>
    Task<IReadOnlyList<GraphEntity>> SearchEntitiesAsync(string query, int maxResults, CancellationToken cancellationToken = default);

    /// <summary>All relations touching <paramref name="entityId"/> (undirected), strongest support first.</summary>
    Task<IReadOnlyList<GraphRelation>> RelationsOfAsync(string entityId, int maxRelations, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GraphEntity>> NeighborsAsync(string entityId, int maxNeighbors, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GraphEntity>> ShortestPathAsync(string fromEntityId, string toEntityId, CancellationToken cancellationToken = default);

    Task<GraphCluster?> ClusterOfAsync(string entityId, CancellationToken cancellationToken = default);
}