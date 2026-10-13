using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Fusion;
using ProvostXMemory.Retrieval;

using Xunit;

namespace ProvostXMemory.RetrievalTests;

/// <summary>QueryPlanRunner: fan-out across target scopes, merge, weak-evidence second pass.</summary>
public sealed class QueryPlanRunnerTests
{
    private sealed class TestScopeResolver : QueryPlanRunner.IScopeResolver
    {
        public MemoryScope Resolve(MemoryScope rootScope, string targetName)
        {
            var separator = targetName.IndexOf('/');
            var kind = Enum.Parse<MemoryScopeKind>(targetName[..separator], ignoreCase: true);
            var id = targetName[(separator + 1)..];
            if (string.Equals(rootScope.Id, id, StringComparison.Ordinal) && rootScope.Kind == kind)
            {
                return rootScope;
            }

            return new MemoryScope(kind, id, rootScope);
        }
    }

    private static (InMemoryScopeIndexRegistry Registry, MemoryScope Root, MemoryScope Child, HashEmbedder Embedder) BuildRig(
        InMemoryStore store,
        string rootId,
        params MemoryRecord[] rootRecords)
    {
        var registry = new InMemoryScopeIndexRegistry();
        var embedder = new HashEmbedder();
        var root = new MemoryScope(MemoryScopeKind.Tenant, rootId);
        var child = new MemoryScope(MemoryScopeKind.Session, $"{rootId}-child", root);

        foreach (var record in rootRecords)
        {
            store.AddAsync(record).GetAwaiter().GetResult();
        }

        RegisterScope(registry, store, embedder, root);
        RegisterScope(registry, store, embedder, child);
        return (registry, root, child, embedder);
    }

    private static void RegisterScope(InMemoryScopeIndexRegistry registry, InMemoryStore store, HashEmbedder embedder, MemoryScope scope)
    {
        // Cheap stub indexes: the plan runner only needs retrieval to resolve through the
        // registry's record provider; a lexical-only rig keeps score semantics deterministic.
        var lexical = new Lexical.LexicalIndex(new Lexical.Bm25Scorer());
        var documents = new List<Lexical.LexicalDocument>();
        var records = store.SnapshotAsync(scope.Kind.ToString(), scope.Id).GetAwaiter().GetResult();
        foreach (var record in records)
        {
            var keyTerms = new List<string> { record.Key };
            keyTerms.AddRange(record.Tags);
            documents.Add(new Lexical.LexicalDocument(record.Id, Lexical.LexicalTokenizer.Tokenize(record.Content), keyTerms, record.Category));
        }

        lexical.Build(documents);
        registry.RegisterLexicalIndex(scope, lexical);
        registry.RegisterRecordProvider(
            scope,
            (targetScope, token) => targetScope.Kind == scope.Kind && targetScope.Id == scope.Id
                ? store.SnapshotAsync(scope.Kind.ToString(), scope.Id, token)
                : Task.FromResult<IReadOnlyList<MemoryRecord>>(Array.Empty<MemoryRecord>()));
    }

    private static QueryPlanRunner CreateRunner(IScopeIndexRegistry registry, HashEmbedder embedder, IQueryTextRewriter? rewriter = null)
    {
        var retriever = new HybridRetriever(registry, embedder, new ReciprocalRankFuser());
        return new QueryPlanRunner(
            retriever,
            registry,
            new ReciprocalRankFuser(),
            new TestScopeResolver(),
            rewriter ?? new KeywordQueryTextRewriter());
    }

    private static MemoryRecord Rec(string scopeId, string id, string content, MemoryScopeKind kind = MemoryScopeKind.Tenant)
        => new(
            Id: id,
            Scope: new MemoryScope(kind, scopeId),
            Key: id,
            Content: content);

    [Fact]
    public async Task Fan_out_across_scopes_merges_results()
    {
        var store = new InMemoryStore();
        var childRecord = Rec("acme-child", "child-record", "quarterly budget review", MemoryScopeKind.Session);
        var (registry, root, child, embedder) = BuildRig(
            store,
            "acme",
            Rec("acme", "root-doc", "annual report figures"));
        await store.AddAsync(childRecord);
        RegisterScope(registry, store, embedder, child);

        var runner = CreateRunner(registry, embedder);
        var plan = new QueryPlan("plan-1", new[]
        {
            new QueryPlanStep("step-root", "search", new[] { "Tenant/acme" }, 5),
            new QueryPlanStep("step-child", "search", new[] { "Session/acme-child" }, 5),
        });

        var result = await runner.RunAsync(plan, "quarterly budget", root);

        Assert.Contains(result.Records, hit => hit.Record.Id == "child-record");
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public async Task Same_record_id_in_two_scopes_never_conflates()
    {
        var store = new InMemoryStore();
        var (registry, root, _, embedder) = BuildRig(
            store,
            "acme",
            Rec("acme", "shared-id", "acme private annual figures"));

        var other = new MemoryScope(MemoryScopeKind.Tenant, "globex");
        await store.AddAsync(Rec("globex", "shared-id", "globex public annual figures"));
        RegisterScope(registry, store, embedder, other);

        var runner = CreateRunner(registry, embedder);
        var plan = new QueryPlan("plan-iso", new[]
        {
            new QueryPlanStep("step-1", "search", new[] { "Tenant/acme", "Tenant/globex" }, 5),
        });

        var result = await runner.RunAsync(plan, "annual figures", root);

        // Both scopes' records survive the merge as distinct hits.
        Assert.Equal(2, result.Records.Count);
        Assert.Contains(result.Records, hit => hit.Record.Id == "shared-id" && hit.Record.Content.Contains("acme", StringComparison.Ordinal));
        Assert.Contains(result.Records, hit => hit.Record.Id == "shared-id" && hit.Record.Content.Contains("globex", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Weak_evidence_triggers_single_rewritten_second_pass()
    {
        var store = new InMemoryStore();
        var (registry, root, _, embedder) = BuildRig(
            store,
            "acme",
            Rec("acme", "doc-1", "kubernetes rollout runbook deployment"));

        var countingRewriter = new CountingRewriter("kubernetes rollout runbook");
        var runner = CreateRunner(registry, embedder, countingRewriter);

        var plan = new QueryPlan("plan-weak", new[]
        {
            new QueryPlanStep("step-1", "search", new[] { "Tenant/acme" }, 5),
        });

        // First pass: the raw query text is a stopword-only string (no index hits → top
        // score 0 → weak evidence → one rewrite-and-rerun).
        var result = await runner.RunAsync(plan, "the a of", root);

        Assert.Equal(1, countingRewriter.Calls);
        Assert.Contains("doc-1", result.Records.Select(hit => hit.Record.Id));
        Assert.Contains(result.Diagnostics, stage => stage.Stage == "plan.pass2");
    }

    [Fact]
    public async Task Strong_first_pass_skips_second_pass()
    {
        var store = new InMemoryStore();
        var (registry, root, _, embedder) = BuildRig(
            store,
            "acme",
            Rec("acme", "doc-1", "kubernetes rollout runbook deployment"));

        var countingRewriter = new CountingRewriter("rewritten");
        var retriever = new HybridRetriever(registry, embedder, new ReciprocalRankFuser());
        var runner = new QueryPlanRunner(
            retriever,
            registry,
            new ReciprocalRankFuser(),
            new TestScopeResolver(),
            rewriter: countingRewriter,
            options: new QueryPlanRunner.QueryPlanRunnerOptions { WeakEvidenceThreshold = 0.001 });

        var plan = new QueryPlan("plan-strong", new[]
        {
            new QueryPlanStep("step-1", "search", new[] { "Tenant/acme" }, 5),
        });

        var strong = await runner.RunAsync(plan, "kubernetes rollout", root);

        Assert.Equal(0, countingRewriter.Calls);
        Assert.NotEmpty(strong.Records);
        Assert.Contains(strong.Diagnostics, stage => stage.Stage == "plan.pass1");
        Assert.DoesNotContain(strong.Diagnostics, stage => stage.Stage == "plan.pass2");
    }

    [Fact]
    public async Task Unknown_target_scope_throws()
    {
        var store = new InMemoryStore();
        var (registry, root, _, embedder) = BuildRig(store, "acme", Rec("acme", "doc-1", "sample content"));

        var runner = CreateRunner(registry, embedder);
        var plan = new QueryPlan("plan-bad", new[]
        {
            new QueryPlanStep("step-1", "search", new[] { "Bogus/acme" }, 5),
        });

        await Assert.ThrowsAsync<ArgumentException>(
            () => runner.RunAsync(plan, "sample", root));
    }

    private sealed class CountingRewriter : IQueryTextRewriter
    {
        private readonly string _rewritten;

        public CountingRewriter(string rewritten)
        {
            _rewritten = rewritten;
        }

        public int Calls { get; private set; }

        public Task<string> RewriteAsync(string text, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(_rewritten);
        }
    }
}