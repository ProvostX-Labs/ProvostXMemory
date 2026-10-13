using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Correction;

namespace ProvostXMemory.CorrectionTests;

/// <summary>Test double: external fetcher returning a fixed snippet list; records calls.</summary>
public sealed class StubExternalFetcher : IExternalFetcher
{
    private readonly IReadOnlyList<string>? _snippets;

    public StubExternalFetcher(params string[] snippets)
    {
        _snippets = snippets;
    }

    public int Calls { get; private set; }

    public Task<IReadOnlyList<string>?> FetchAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(_snippets);
    }
}