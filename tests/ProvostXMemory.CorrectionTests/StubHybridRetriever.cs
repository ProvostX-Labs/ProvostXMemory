using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Retrieval;

namespace ProvostXMemory.CorrectionTests;

/// <summary>Test double: hybrid retriever replaying queued results; records each query text.</summary>
public sealed class StubHybridRetriever : IHybridRetriever
{
    private readonly Queue<RetrievalResult> _results;

    public StubHybridRetriever(params RetrievalResult[] results)
    {
        _results = new Queue<RetrievalResult>(results);
    }

    public List<string> QueryTexts { get; } = new();

    public Task<RetrievalResult> RetrieveAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
    {
        QueryTexts.Add(query.Text);
        return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : new RetrievalResult(Array.Empty<ScoredMemoryRecord>()));
    }
}