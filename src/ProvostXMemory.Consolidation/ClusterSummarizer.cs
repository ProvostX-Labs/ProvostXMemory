using System.Security.Cryptography;
using System.Text;
using System.Globalization;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Consolidation;

/// <summary>
/// Builds a summary <see cref="MemoryRecord"/> from a near-duplicate cluster: the
/// concatenated member keys plus deterministic representative excerpts, category
/// "summary", and Meta carrying the cluster-summary flag, member count, cluster id and
/// member ids. Cluster id = sha256 over ordinal member ids, so the same cluster always
/// maps to the same summary id (idempotent re-consolidation).
/// </summary>
public sealed class ClusterSummarizer : IClusterSummarizer
{
    /// <summary>Category assigned to emitted summary records.</summary>
    public const string SummaryCategory = "summary";

    private const string SummaryMetaFlag = "cluster-summary";
    private const string ClusterIdMetaKey = "cluster-id";
    private const string MemberCountMetaKey = "cluster-member-count";
    private const string MemberIdsMetaKey = "cluster-member-ids";

    private readonly ConsolidationTuning _tuning;

    public ClusterSummarizer(ConsolidationTuning? tuning = null)
    {
        _tuning = tuning ?? new ConsolidationTuning();
    }

    public Task<MemoryRecord> SummarizeAsync(string scopeKind, string scopeId, IReadOnlyList<MemoryRecord> cluster, CancellationToken cancellationToken = default)
    {
        ValidateScopeParameters(scopeKind, scopeId);
        ArgumentNullException.ThrowIfNull(cluster);
        cancellationToken.ThrowIfCancellationRequested();
        if (cluster.Count == 0)
        {
            throw new ArgumentException("Cluster must contain at least one record.", nameof(cluster));
        }

        // Deterministic order regardless of caller-supplied sequence: ordinal by id.
        var members = cluster.OrderBy(record => record.Id, StringComparer.Ordinal).ToList();

        var clusterId = ClusterId(members);
        var keys = string.Join(" | ", members.Select(record => record.Key).Distinct(StringComparer.Ordinal));
        var excerpts = members
            .Take(_tuning.SummaryExcerptLimit)
            .Select(member => $"- {member.Id} | {member.Key}: {Truncate(member.Content, _tuning.SummaryExcerptLength)}");
        var content = $"[cluster {keys}]\n{string.Join("\n", excerpts)}";

        var memberIds = string.Join(",", members.Select(record => record.Id));
        var scope = new MemoryScope(ParseScopeKind(scopeKind), scopeId);
        var record = new MemoryRecord(
            Id: SummaryRecordId(clusterId),
            Scope: scope,
            Key: keys,
            Content: content,
            Category: SummaryCategory)
        {
            Tags = members
                .SelectMany(member => member.Tags)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToList(),
            Meta = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [SummaryMetaFlag] = "true",
                [ClusterIdMetaKey] = clusterId,
                [MemberCountMetaKey] = members.Count.ToString(CultureInfo.InvariantCulture),
                [MemberIdsMetaKey] = memberIds,
            },
        };

        return Task.FromResult(record);
    }

    /// <summary>Stable summary id derived from the cluster id.</summary>
    private static string SummaryRecordId(string clusterId) => $"summary-{clusterId}";

    /// <summary>sha256 over ordinal-sorted member ids — deterministic across runs and processes.</summary>
    private static string ClusterId(IReadOnlyList<MemoryRecord> orderedMembers)
    {
        var material = string.Join("\n", orderedMembers.Select(record => record.Id));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Truncate(string text, int maxLength)
    {
        var normalized = text
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "…";
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

    // Pure parser for scope kinds; unknown values degrade to Custom.
    private static MemoryScopeKind ParseScopeKind(string value)
    {
        return Enum.TryParse<MemoryScopeKind>(value, ignoreCase: true, out var kind) ? kind : MemoryScopeKind.Custom;
    }
}