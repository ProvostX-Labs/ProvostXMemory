using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Storage;

namespace ProvostXMemory.Graph;

/// <summary>
/// JSONL-backed graph store: <c>graph/entities.jsonl</c>, <c>graph/relations.jsonl</c> and
/// <c>graph/clusters.jsonl</c> per scope directory (via <see cref="MemoryRootLayout.GraphPath"/>).
/// Paths resolve only through the layout, so cross-scope access stays structurally impossible.
/// Every mutation rewrites the affected JSONL file transactionally via <see cref="AtomicFileWriter"/>.
/// </summary>
public sealed class JsonlMemoryGraphStore : IMemoryGraphStore
{
    // Pure factory of immutable JSON serializer options (camelCase JSONL documents); no shared mutable state.
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private const string EntitiesFileName = "entities.jsonl";
    private const string RelationsFileName = "relations.jsonl";
    private const string ClustersFileName = "clusters.jsonl";

    private readonly MemoryRootLayout _layout;
    private readonly AtomicFileWriter _writer;
    private readonly string _scopeKind;
    private readonly string _scopeId;

    /// <summary>Creates a graph store pinned to one scope at construction.</summary>
    public JsonlMemoryGraphStore(MemoryRootLayout layout, string scopeKind, string scopeId, AtomicFileWriter? writer = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _scopeKind = RequireSegment(scopeKind, nameof(scopeKind));
        _scopeId = RequireSegment(scopeId, nameof(scopeId));
        _layout = layout;
        _writer = writer ?? new AtomicFileWriter(fsync: false); // pure factory default; no shared mutable state
    }

    /// <summary>Convenience overload: pins the store to the primary scope of <paramref name="scope"/>.</summary>
    public JsonlMemoryGraphStore(MemoryRootLayout layout, MemoryScope scope, AtomicFileWriter? writer = null)
        : this(layout, scope.Kind.ToString(), scope.Id, writer)
    {
    }

    /// <summary>The scope kind this store instance is pinned to.</summary>
    public string ScopeKind => _scopeKind;

    /// <summary>The scope id this store instance is pinned to.</summary>
    public string ScopeId => _scopeId;

    /// <summary>The resolved path of the scope's entities file.</summary>
    public string EntitiesPath => _layout.GraphPath(_scopeKind, _scopeId, EntitiesFileName);

    /// <summary>The resolved path of the scope's relations file.</summary>
    public string RelationsPath => _layout.GraphPath(_scopeKind, _scopeId, RelationsFileName);

    /// <summary>The resolved path of the scope's clusters file.</summary>
    public string ClustersPath => _layout.GraphPath(_scopeKind, _scopeId, ClustersFileName);

    /// <summary>
    /// Inserts or merges the entity: when an entity with the same id exists, <see cref="GraphEntity.RecordIds"/>
    /// and <see cref="GraphEntity.Aliases"/> are unioned and the existing name is kept, so index-time
    /// per-record upserts accumulate weight (= number of supporting records).
    /// </summary>
    public async Task UpsertEntityAsync(GraphEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        var entries = await ReadAllAsync<GraphEntityDto>(EntitiesPath, cancellationToken).ConfigureAwait(false);
        var index = FindIndexById(entries, entity.EntityId);
        if (index < 0)
        {
            entries.Add(GraphEntityDto.From(entity));
        }
        else
        {
            entries[index] = entries[index] with
            {
                Aliases = entries[index].Aliases
                    .Concat(entity.Aliases)
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(a => a, StringComparer.Ordinal)
                    .ToList(),
                RecordIds = entries[index].RecordIds
                    .Concat(entity.RecordIds)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList(),
            };
        }

        await WriteAllAsync(EntitiesPath, entries, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Inserts or merges the relation: when a relation with the same unordered pair + label exists,
    /// <see cref="GraphRelation.RecordIds"/> is unioned, so index-time per-record upserts accumulate
    /// weight (= number of supporting records).
    /// </summary>
    public async Task UpsertRelationAsync(GraphRelation relation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relation);
        cancellationToken.ThrowIfCancellationRequested();

        var entries = await ReadAllAsync<GraphRelationDto>(RelationsPath, cancellationToken).ConfigureAwait(false);
        var index = FindIndexByRelation(entries, relation);
        if (index < 0)
        {
            entries.Add(GraphRelationDto.From(relation));
        }
        else
        {
            entries[index] = entries[index] with
            {
                RecordIds = entries[index].RecordIds
                    .Concat(relation.RecordIds)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList(),
            };
        }

        await WriteAllAsync(RelationsPath, entries, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpsertClusterAsync(GraphCluster cluster, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        cancellationToken.ThrowIfCancellationRequested();

        var entries = await ReadAllAsync<GraphClusterDto>(ClustersPath, cancellationToken).ConfigureAwait(false);
        var dto = GraphClusterDto.From(cluster);
        var index = FindIndexById(entries, cluster.ClusterId);
        if (index < 0)
        {
            entries.Add(dto);
        }
        else
        {
            entries[index] = dto; // replace: consolidation output is authoritative, no union
        }

        // Deterministic file order keeps rewrites byte-stable across passes.
        entries.Sort(static (a, b) => string.CompareOrdinal(a.EntryId, b.EntryId));
        await WriteAllAsync(ClustersPath, entries, cancellationToken).ConfigureAwait(false);
    }

    public Task<GraphEntity?> GetEntityAsync(string entityId, CancellationToken cancellationToken = default)
    {
        ValidateEntityId(entityId);
        return GetEntityCoreAsync(entityId, cancellationToken);
    }

    public async Task<IReadOnlyList<GraphEntity>> SearchEntitiesAsync(string query, int maxResults, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return EmptyEntities(); // pure empty factory
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (maxResults <= 0)
        {
            return EmptyEntities();
        }

        var queryTokens = new HashSet<string>(Tokenize(query), StringComparer.OrdinalIgnoreCase);
        if (queryTokens.Count == 0)
        {
            return EmptyEntities();
        }

        var entities = await ReadAllAsync<GraphEntityDto>(EntitiesPath, cancellationToken).ConfigureAwait(false);

        // Token-overlap search across name + aliases; ties break deterministically by entity id.
        var scored = new List<(GraphEntityDto Dto, int Overlap)>();
        foreach (var dto in entities)
        {
            var texts = new List<string> { dto.Name };
            texts.AddRange(dto.Aliases);
            var overlap = 0;
            foreach (var text in texts)
            {
                foreach (var token in Tokenize(text))
                {
                    if (queryTokens.Contains(token))
                    {
                        overlap++;
                    }
                }
            }

            if (overlap > 0)
            {
                scored.Add((dto, overlap));
            }
        }

        return scored
            .OrderByDescending(pair => pair.Overlap)
            .ThenBy(pair => pair.Dto.EntityId, StringComparer.Ordinal)
            .Take(maxResults)
            .Select(pair => pair.Dto.ToEntity())
            .ToList();
    }

    public async Task<IReadOnlyList<GraphRelation>> RelationsOfAsync(string entityId, int maxRelations, CancellationToken cancellationToken = default)
    {
        ValidateEntityId(entityId);
        cancellationToken.ThrowIfCancellationRequested();
        if (maxRelations <= 0)
        {
            return Array.Empty<GraphRelation>(); // pure empty factory
        }

        var relations = await ReadAllAsync<GraphRelationDto>(RelationsPath, cancellationToken).ConfigureAwait(false);
        return relations
            .Where(r => string.Equals(r.FromEntityId, entityId, StringComparison.Ordinal)
                        || string.Equals(r.ToEntityId, entityId, StringComparison.Ordinal))
            .OrderByDescending(r => r.RecordIds.Count)
            .ThenBy(r => r.RelationId, StringComparer.Ordinal)
            .Take(maxRelations)
            .Select(dto => dto.ToRelation())
            .ToList();
    }

    public async Task<IReadOnlyList<GraphEntity>> NeighborsAsync(string entityId, int maxNeighbors, CancellationToken cancellationToken = default)
    {
        ValidateEntityId(entityId);
        cancellationToken.ThrowIfCancellationRequested();
        if (maxNeighbors <= 0)
        {
            return EmptyEntities(); // pure empty factory
        }

        var relations = await ReadAllAsync<GraphRelationDto>(RelationsPath, cancellationToken).ConfigureAwait(false);
        var neighborIds = CollectNeighborIds(relations, entityId);
        if (neighborIds.Count == 0)
        {
            return EmptyEntities();
        }

        // Tie-break deterministically: strongest support first, then entity id.
        var ordered = neighborIds
            .OrderByDescending(n => n.Support)
            .ThenBy(n => n.EntityId, StringComparer.Ordinal)
            .Take(maxNeighbors);

        var entities = await ReadAllAsync<GraphEntityDto>(EntitiesPath, cancellationToken).ConfigureAwait(false);
        var byId = ToDictionaryById(entities);
        var result = new List<GraphEntity>();
        foreach (var neighbor in ordered)
        {
            result.Add(byId.TryGetValue(neighbor.EntityId, out var dto)
                ? dto.ToEntity()
                : new GraphEntity(neighbor.EntityId, neighbor.EntityId, Array.Empty<string>(), Array.Empty<string>()));
        }

        return result;
    }

    public async Task<IReadOnlyList<GraphEntity>> ShortestPathAsync(string fromEntityId, string toEntityId, CancellationToken cancellationToken = default)
    {
        ValidateEntityId(fromEntityId);
        ValidateEntityId(toEntityId);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.Equals(fromEntityId, toEntityId, StringComparison.Ordinal))
        {
            var start = await TryGetEntityCoreAsync(fromEntityId, cancellationToken).ConfigureAwait(false);
            return start is null ? EmptyEntities() : new List<GraphEntity> { start };
        }

        var relations = await ReadAllAsync<GraphRelationDto>(RelationsPath, cancellationToken).ConfigureAwait(false);
        var adjacency = BuildAdjacency(relations);
        if (adjacency.Count == 0)
        {
            return EmptyEntities();
        }

        // BFS with deterministic visit order (sorted expansion), recording parents on first discovery.
        var parent = new Dictionary<string, string>(StringComparer.Ordinal) { [fromEntityId] = string.Empty };
        var queue = new Queue<string>();
        queue.Enqueue(fromEntityId);
        while (queue.Count > 0 && !parent.ContainsKey(toEntityId))
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var edges))
            {
                continue;
            }

            foreach (var neighbor in edges.Select(e => e.EntityId).OrderBy(id => id, StringComparer.Ordinal))
            {
                if (parent.ContainsKey(neighbor))
                {
                    continue;
                }

                parent[neighbor] = current;
                if (string.Equals(neighbor, toEntityId, StringComparison.Ordinal))
                {
                    break;
                }

                queue.Enqueue(neighbor);
            }
        }

        if (!parent.ContainsKey(toEntityId))
        {
            return EmptyEntities();
        }

        var pathIds = new List<string>();
        var cursor = toEntityId;
        while (!string.IsNullOrEmpty(cursor))
        {
            pathIds.Add(cursor);
            cursor = parent[cursor];
        }

        pathIds.Reverse();

        var entities = await ReadAllAsync<GraphEntityDto>(EntitiesPath, cancellationToken).ConfigureAwait(false);
        var byId = ToDictionaryById(entities);
        var path = new List<GraphEntity>();
        foreach (var id in pathIds)
        {
            path.Add(byId.TryGetValue(id, out var dto)
                ? dto.ToEntity()
                : new GraphEntity(id, id, Array.Empty<string>(), Array.Empty<string>()));
        }

        return path;
    }

    public Task<GraphCluster?> ClusterOfAsync(string entityId, CancellationToken cancellationToken = default)
    {
        ValidateEntityId(entityId);
        return ClusterOfCoreAsync(entityId, cancellationToken);
    }

    private async Task<GraphEntity?> GetEntityCoreAsync(string entityId, CancellationToken cancellationToken)
    {
        var entities = await ReadAllAsync<GraphEntityDto>(EntitiesPath, cancellationToken).ConfigureAwait(false);
        var match = entities.FirstOrDefault(e => string.Equals(e.EntityId, entityId, StringComparison.Ordinal));
        return match?.ToEntity();
    }

    private async Task<GraphEntity?> TryGetEntityCoreAsync(string entityId, CancellationToken cancellationToken)
    {
        return await GetEntityCoreAsync(entityId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<GraphCluster?> ClusterOfCoreAsync(string entityId, CancellationToken cancellationToken)
    {
        var clusters = await ReadAllAsync<GraphClusterDto>(ClustersPath, cancellationToken).ConfigureAwait(false);
        var match = clusters.FirstOrDefault(c => c.EntityIds.Contains(entityId, StringComparer.Ordinal));
        return match?.ToCluster();
    }

    // Pure mapping from relation list to undirected adjacency (BSP shortest path); no shared mutable state.
    private static Dictionary<string, List<(string EntityId, int Support)>> BuildAdjacency(List<GraphRelationDto> relations)
    {
        var adjacency = new Dictionary<string, List<(string, int)>>(StringComparer.Ordinal);
        foreach (var relation in relations)
        {
            AddEdge(adjacency, relation.FromEntityId, relation.ToEntityId, relation.RecordIds.Count);
            if (!string.Equals(relation.FromEntityId, relation.ToEntityId, StringComparison.Ordinal))
            {
                AddEdge(adjacency, relation.ToEntityId, relation.FromEntityId, relation.RecordIds.Count);
            }
        }

        return adjacency;
    }

    // Pure mapping from relation list to neighbor ids with best support; no shared mutable state.
    private static List<(string EntityId, int Support)> CollectNeighborIds(List<GraphRelationDto> relations, string entityId)
    {
        var best = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var relation in relations)
        {
            var other = default(string);
            if (string.Equals(relation.FromEntityId, entityId, StringComparison.Ordinal))
            {
                other = relation.ToEntityId;
            }
            else if (string.Equals(relation.ToEntityId, entityId, StringComparison.Ordinal))
            {
                other = relation.FromEntityId;
            }

            if (other is null)
            {
                continue;
            }

            var support = relation.RecordIds.Count;
            if (!best.TryGetValue(other, out var current) || support > current)
            {
                best[other] = support;
            }
        }

        return best.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    // Pure adjacency insert for an undirected edge, last-write-wins on duplicates; no shared mutable state.
    private static void AddEdge(Dictionary<string, List<(string EntityId, int Support)>> adjacency, string from, string to, int support)
    {
        if (!adjacency.TryGetValue(from, out var edges))
        {
            edges = new List<(string, int)>();
            adjacency[from] = edges;
        }

        edges.RemoveAll(edge => string.Equals(edge.EntityId, to, StringComparison.Ordinal));
        edges.Add((to, support));
    }

    private static Dictionary<string, GraphEntityDto> ToDictionaryById(List<GraphEntityDto> entities)
    {
        var map = new Dictionary<string, GraphEntityDto>(StringComparer.Ordinal);
        foreach (var entity in entities)
        {
            map[entity.EntityId] = entity;
        }

        return map;
    }

    private static int FindIndexById<T>(List<T> entries, string id) where T : IGraphEntryIdentified
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].EntryId, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindIndexByRelation(List<GraphRelationDto> entries, GraphRelation relation)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var samePair =
                (string.Equals(entry.FromEntityId, relation.FromEntityId, StringComparison.Ordinal)
                 && string.Equals(entry.ToEntityId, relation.ToEntityId, StringComparison.Ordinal))
                || (string.Equals(entry.FromEntityId, relation.ToEntityId, StringComparison.Ordinal)
                    && string.Equals(entry.ToEntityId, relation.FromEntityId, StringComparison.Ordinal));
            if (samePair && string.Equals(entry.Label, relation.Label, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private async Task<List<T>> ReadAllAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new List<T>();
        }

        var entries = new List<T>();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1 << 16);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize<T>(line, JsonOptions);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    private async Task WriteAllAsync<T>(string path, IReadOnlyList<T> entries, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _writer.CleanupStaleTempFiles(path);

        var builder = new StringBuilder();
        foreach (var entry in entries)
        {
            builder.Append(JsonSerializer.Serialize(entry, typeof(T), JsonOptions));
            builder.Append('\n');
        }

        _writer.Write(path, builder.ToString());
        await Task.CompletedTask.ConfigureAwait(false);
    }

    // Pure empty-list factory; no shared mutable state.
    private static IReadOnlyList<GraphEntity> EmptyEntities() => Array.Empty<GraphEntity>();

    private static string RequireSegment(string value, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", paramName);
        }

        // Path safety is enforced by MemoryRootLayout; here we only reject empty/whitespace ids.
        return value;
    }

    private static void ValidateEntityId(string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            throw new ArgumentException("Entity id is required.", nameof(entityId));
        }
    }

    // Pure lowercase-alphanumeric token factory for a text; no shared mutable state.
    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var builder = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (builder.Length > 0)
            {
                tokens.Add(builder.ToString());
                builder.Clear();
            }
        }

        if (builder.Length > 0)
        {
            tokens.Add(builder.ToString());
        }

        return tokens;
    }

    // Pure factory: immutable serializer options (camelCase JSONL documents).
    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
    }

    private sealed record GraphEntityDto(
        string EntityId,
        string Name,
        IReadOnlyList<string> Aliases,
        IReadOnlyList<string> RecordIds) : IGraphEntryIdentified
    {
        public string EntryId => EntityId;

        // Pure mapping from persistence DTO to domain record.
        public GraphEntity ToEntity() => new(EntityId, Name, Aliases, RecordIds);

        // Pure mapping from domain record to persistence DTO.
        public static GraphEntityDto From(GraphEntity entity) => new(entity.EntityId, entity.Name, entity.Aliases, entity.RecordIds);
    }

    private sealed record GraphRelationDto(
        string RelationId,
        string FromEntityId,
        string ToEntityId,
        string Label,
        IReadOnlyList<string> RecordIds) : IGraphEntryIdentified
    {
        public string EntryId => RelationId;

        // Pure mapping from persistence DTO to domain record.
        public GraphRelation ToRelation() => new(RelationId, FromEntityId, ToEntityId, Label, RecordIds);

        // Pure mapping from domain record to persistence DTO.
        public static GraphRelationDto From(GraphRelation relation) =>
            new(relation.RelationId, relation.FromEntityId, relation.ToEntityId, relation.Label, relation.RecordIds);
    }

    private sealed record GraphClusterDto(
        string ClusterId,
        IReadOnlyList<string> EntityIds,
        string? SummaryRecordId) : IGraphEntryIdentified
    {
        public string EntryId => ClusterId;

        // Pure mapping from persistence DTO to domain record.
        public GraphCluster ToCluster() => new(ClusterId, EntityIds, SummaryRecordId);

        // Pure mapping from domain record to persistence DTO.
        public static GraphClusterDto From(GraphCluster cluster) =>
            new(cluster.ClusterId, cluster.EntityIds, cluster.SummaryRecordId);
    }

    private interface IGraphEntryIdentified
    {
        string EntryId { get; }
    }
}