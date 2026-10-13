using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Eval;

using Xunit;

namespace ProvostXMemory.EvalTests;

/// <summary>Corpus determinism (same seed → same hash), cluster balance, expected-match derivability.</summary>
public sealed class DeterministicEvalCorpusTests
{
    [Fact]
    public void Same_seed_same_parameters_same_hash_and_content()
    {
        var first = new DeterministicEvalCorpus(seed: 42, scopeCount: 3, recordCountPerScope: 24);
        var second = new DeterministicEvalCorpus(seed: 42, scopeCount: 3, recordCountPerScope: 24);

        Assert.Equal(first.DeterminismHash(), second.DeterminismHash());
        Assert.Equal(
            first.Records.Select(record => record.Record.Content),
            second.Records.Select(record => record.Record.Content));
        Assert.Equal(
            first.Records.Select(record => record.Record.Id),
            second.Records.Select(record => record.Record.Id));
    }

    [Fact]
    public void Different_seed_different_hash()
    {
        var first = new DeterministicEvalCorpus(seed: 42, scopeCount: 2, recordCountPerScope: 12);
        var second = new DeterministicEvalCorpus(seed: 43, scopeCount: 2, recordCountPerScope: 12);

        Assert.NotEqual(first.DeterminismHash(), second.DeterminismHash());
    }

    [Fact]
    public void Determinism_hash_is_stable_within_a_process_and_well_formed()
    {
        var corpus = new DeterministicEvalCorpus(seed: 7, scopeCount: 2, recordCountPerScope: 8);

        var first = corpus.DeterminismHash();
        var second = corpus.DeterminismHash();

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length); // SHA-256 hex
    }

    [Fact]
    public void Record_counts_and_cluster_balance()
    {
        const int scopes = 4;
        const int perScope = 16;
        const int clusters = 8;
        var corpus = new DeterministicEvalCorpus(seed: 5, scopes, perScope, clusters);

        Assert.Equal(scopes * perScope, corpus.TotalRecords);
        foreach (var scope in corpus.Scopes)
        {
            for (var cluster = 0; cluster < clusters; cluster++)
            {
                Assert.Equal(perScope / clusters, corpus.RecordIdsOf(scope, cluster).Count);
            }
        }
    }

    [Fact]
    public void Scopes_are_distinct_and_isolated_by_construction()
    {
        var corpus = new DeterministicEvalCorpus(seed: 11, scopeCount: 3, recordCountPerScope: 8);

        Assert.Equal(3, corpus.Scopes.Count);
        var ids = corpus.Scopes.Select(scope => scope.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        // no record ever appears under a foreign scope
        foreach (var record in corpus.Records)
        {
            var sameScope = corpus.Records.Count(other =>
                MemoryScopeComparer.Ordinal.Equals(other.Scope, record.Scope)
                && other.Record.Id == record.Record.Id);
            Assert.Equal(1, sameScope);
        }
    }

    [Fact]
    public void Every_record_content_contains_its_cluster_terms_and_leak_check_via_case_set()
    {
        // The golden case set must be derivable: every relevant id must belong to the
        // query's cluster AND the case's scope (ids intentionally repeat across scopes,
        // so the lookup is (scope, id)-qualified).
        var corpus = new DeterministicEvalCorpus(seed: 99, scopeCount: 2, recordCountPerScope: 16);
        var caseSet = corpus.BuildEvalCaseSet();

        Assert.Equal(2 * 8, caseSet.Cases.Count); // 2 scopes × 8 clusters (recordCount/cluster)
        foreach (var evalCase in caseSet.Cases)
        {
            Assert.NotEmpty(evalCase.RelevantIds);
            var keyword = evalCase.Query.Split(' ')[0];
            var relevantSet = evalCase.RelevantIds.ToHashSet(StringComparer.Ordinal);
            foreach (var record in corpus.Records)
            {
                if (relevantSet.Contains(record.Record.Id))
                {
                    // Relevant ids are scope-resolved by construction: EVERY corpus record
                    // bearing a relevant id must live in the case's own scope.
                    Assert.True(
                        MemoryScopeComparer.Ordinal.Equals(record.Record.Scope, evalCase.QueryScope),
                        $"Case {evalCase.Id}: relevant record {record.Record.Id} from foreign scope.");
                }
            }

            // Each relevant id resolves to exactly one corpus record (in-scope instance).
            foreach (var relevantId in evalCase.RelevantIds)
            {
                Assert.Single(corpus.Records, record => record.Record.Id == relevantId);
            }

            // The cluster's keyword actually occurs in some record of the same scope.
            var keywordPresent = corpus.Records.Any(record =>
                MemoryScopeComparer.Ordinal.Equals(record.Record.Scope, evalCase.QueryScope)
                && record.Record.Content.Contains(keyword, StringComparison.Ordinal));
            Assert.True(keywordPresent, $"Case {evalCase.Id}: cluster keyword '{keyword}' missing.");
        }
    }

    [Fact]
    public void Invalid_construction_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicEvalCorpus(1, 0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicEvalCorpus(1, 2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicEvalCorpus(1, 2, 4, clusterCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicEvalCorpus(1, 2, 4, clusterCount: 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicEvalCorpus(1, 2, 4, vectorDim: 8));
    }
}
