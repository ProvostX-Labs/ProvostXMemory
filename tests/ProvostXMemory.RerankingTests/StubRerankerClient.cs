using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Reranking;

namespace ProvostXMemory.RerankingTests;

/// <summary>Test double: reranker client replaying a fixed best-first id order; records requests.</summary>
public sealed class StubRerankerClient : IRerankerClient
{
    private readonly IReadOnlyList<string> _orderedIds;

    public StubRerankerClient(params string[] orderedIds)
    {
        _orderedIds = orderedIds;
    }

    public int Calls { get; private set; }

    public List<IReadOnlyList<string>> Requests { get; } = new();

    public string? LastQueryText { get; private set; }

    public Task<IReadOnlyList<string>> RerankAsync(string query, IReadOnlyList<string> candidateIds, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastQueryText = query;
        Requests.Add(candidateIds);
        return Task.FromResult(_orderedIds);
    }
}