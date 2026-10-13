using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Eval;

/// <summary>
/// Seeded synthetic corpus: N records per scope across M scopes, round-robin assigned to
/// keyword clusters (exact balance) so every scope/cluster pair yields the same number of
/// expected matches per query. Content
/// is a seeded shuffle of the record's cluster terms plus filler words, one hard-negative
/// term from a seeded foreign cluster, and one seeded noise token — derivable relevance
/// without an LLM: the expected matches for a cluster query are exactly that cluster's
/// records. Identical seeds yield byte-identical corpora, verified via <see cref="DeterminismHash"/>.
/// </summary>
public sealed class DeterministicEvalCorpus
{
    /// <summary>Keyword clusters; each record's content is a seeded shuffle of cluster terms.</summary>
    private static readonly string[][] Clusters =
    {
        new[] { "kubernetes", "cluster", "rollout", "helm", "chart", "release" },
        new[] { "postgres", "migration", "index", "vacuum", "tablespace", "lock" },
        new[] { "invoice", "billing", "stripe", "refund", "chargeback", "receipt" },
        new[] { "passport", "visa", "embassy", "schengen", "residence", "permit" },
        new[] { "sourdough", "starter", "ferment", "crumb", "banneton", "oven" },
        new[] { "telescope", "jupiter", "saturn", "occultation", "asteroid", "eyepiece" },
        new[] { "docker", "compose", "volume", "network", "registry", "layer" },
        new[] { "piano", "chopin", "nocturne", "pedal", "polyrhythm", "etude" },
    };

    private const int DefaultVectorDim = 128;
    private const string FillerWords = "notes summary context background detail remark entry";

    private static readonly string[] ExoticNoiseTokens =
    {
        "qxzv", "wyjk", "zxqk", "vjqx", "kwzy", "jqwz", "xzvk", "yqwm",
    };

    private readonly IReadOnlyList<MemoryScope> _scopes;
    private readonly IReadOnlyList<CorpusRecord> _records;

    public DeterministicEvalCorpus(
        int seed,
        int scopeCount,
        int recordCountPerScope,
        int clusterCount = 8,
        int vectorDim = DefaultVectorDim)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(scopeCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(recordCountPerScope, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(clusterCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(clusterCount, Clusters.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(vectorDim, 16);

        Seed = seed;
        VectorDim = vectorDim;
        ClusterCountUsed = clusterCount;
        _scopes = BuildScopes(scopeCount);
        _records = BuildRecords(seed, _scopes, recordCountPerScope, clusterCount);
    }

    public int Seed { get; }

    public int VectorDim { get; }

    /// <summary>Cluster count in effect at generation time (≤ built-in cluster table size).</summary>
    public int ClusterCountUsed { get; }

    /// <summary>All generated scopes (no parent scopes; isolation tested at run time).</summary>
    public IReadOnlyList<MemoryScope> Scopes => _scopes;

    /// <summary>All records across every scope (each record carries its own scope).</summary>
    public IReadOnlyList<CorpusRecord> Records => _records;

    /// <summary>Total record count across all scopes.</summary>
    public int TotalRecords => _records.Count;

    /// <summary>Record ids of one cluster within one scope, generation order.</summary>
    public IReadOnlyList<string> RecordIdsOf(MemoryScope scope, int clusterIndex)
    {
        return _records
            .Where(record =>
                MemoryScopeComparer.Ordinal.Equals(record.Scope, scope)
                && record.ClusterIndex == clusterIndex)
            .Select(record => record.Record.Id)
            .ToList();
    }

    /// <summary>
    /// Stable SHA-256 digest over the seed, parameters, and every record in generation
    /// order — same seed and parameters produce the same hash byte-for-byte across runs.
    /// </summary>
    public string DeterminismHash()
    {
        var payload = new StringBuilder("provostx-eval-corpus\x1f");
        payload.Append(CultureInfo.InvariantCulture, $"{Seed}\x1f{VectorDim}\x1f{_scopes.Count}\x1f{TotalRecords}\x1f{ClusterCountUsed}");
        foreach (var scope in _scopes)
        {
            payload.Append(CultureInfo.InvariantCulture, $"\x1fS:{scope.Kind}:{scope.Id}");
        }

        foreach (var record in _records)
        {
            payload.Append(CultureInfo.InvariantCulture, $"\x1fR:{record.Record.Id}|{record.ClusterIndex}|{record.Record.Content}");
        }

        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString())));
    }

    /// <summary>
    /// Builds one golden eval case per scope per cluster: the query is the cluster's terms
    /// and the expected matches are exactly that cluster's records in that scope.
    /// </summary>
    public EvalCaseSet BuildEvalCaseSet()
    {
        var cases = new List<EvalCase>();
        for (var scopeIndex = 0; scopeIndex < _scopes.Count; scopeIndex++)
        {
            var scope = _scopes[scopeIndex];
            for (var cluster = 0; cluster < ClusterCountUsed; cluster++)
            {
                var relevant = RecordIdsOf(scope, cluster);
                if (relevant.Count == 0)
                {
                    continue;
                }

                cases.Add(new EvalCase(
                    id: $"s{scopeIndex:D2}-c{cluster:D2}",
                    query: string.Join(' ', Clusters[cluster]),
                    queryScope: scope,
                    relevantIds: relevant));
            }
        }

        return new EvalCaseSet($"deterministic-{Seed}", cases);
    }

    public sealed record CorpusRecord(
        MemoryScope Scope,
        MemoryRecord Record,
        int ClusterIndex);

    // Pure scope factory; intentionally static per architecture rule.
    private static IReadOnlyList<MemoryScope> BuildScopes(int scopeCount)
    {
        var scopes = new List<MemoryScope>(scopeCount);
        for (var i = 0; i < scopeCount; i++)
        {
            scopes.Add(new MemoryScope(MemoryScopeKind.Purpose, $"eval-scope-{i:D2}"));
        }

        return scopes;
    }

    private static IReadOnlyList<CorpusRecord> BuildRecords(
        int seed,
        IReadOnlyList<MemoryScope> scopes,
        int recordCountPerScope,
        int clusterCount)
    {
        var records = new List<CorpusRecord>(scopes.Count * recordCountPerScope);
        for (var scopeIndex = 0; scopeIndex < scopes.Count; scopeIndex++)
        {
            var scope = scopes[scopeIndex];
            // Per-scope RNG derived from seed + scope identity: scope A's content never
            // depends on scope B's generation order. StableScopeHash is process-stable
            // (FNV-1a, not string.GetHashCode which is hash-seed randomized per process).
            var random = new Random((int)(uint)(seed ^ StableScopeHash(scope)));
            for (var i = 0; i < recordCountPerScope; i++)
            {
                var cluster = i % clusterCount; // round-robin: exact cluster balance
                var content = BuildContent(random, cluster, clusterCount);
                // Globally-unique scope-qualified id: golden relevance is unambiguous in
                // reports/traces even though scopes repeat the same id scheme.
                var id = RecordId(scopeIndex, cluster, i);
                var record = new MemoryRecord(
                    Id: id,
                    Scope: scope,
                    Key: id,
                    Content: content)
                {
                    Tags = new[] { ClusterTag(cluster) },
                };
                records.Add(new CorpusRecord(scope, record, cluster));
            }
        }

        return records;
    }

    private static string BuildContent(Random random, int cluster, int clusterCount)
    {
        var terms = Clusters[cluster].ToArray();
        // Deterministic Fisher-Yates shuffle of the cluster terms.
        for (var i = terms.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (terms[i], terms[j]) = (terms[j], terms[i]);
        }

        // One hard-negative term from a seeded foreign cluster + one seeded noise token.
        var foreign = (cluster + 1 + random.Next(clusterCount - 1)) % clusterCount;
        var foreignTerms = Clusters[foreign];
        var foreignTerm = foreignTerms[random.Next(foreignTerms.Length)];
        var noise = ExoticNoiseTokens[random.Next(ExoticNoiseTokens.Length)];

        return $"{string.Join(' ', terms)} {FillerWords} {foreignTerm} {noise}";
    }

    private static string RecordId(int scopeIndex, int cluster, int index)
        => $"s{scopeIndex:D2}-c{cluster:D2}-r{index:D3}";

    private static string ClusterTag(int cluster) => $"cluster-{cluster:D2}";

    // Process-stable FNV-1a 64 hash of scope identity for seed derivation (string.GetHashCode
    // is randomized per process and would break cross-run determinism); intentionally static.
    private static long StableScopeHash(MemoryScope scope)
    {
        const ulong FnvOffsetBasis = 0xcbf29ce484222325UL;
        const ulong FnvPrime = 0x100000001b3UL;
        var hash = FnvOffsetBasis;
        foreach (var ch in $"{scope.Kind}:{scope.Id}")
        {
            hash ^= ch;
            hash *= FnvPrime;
        }

        return unchecked((long)hash);
    }
}