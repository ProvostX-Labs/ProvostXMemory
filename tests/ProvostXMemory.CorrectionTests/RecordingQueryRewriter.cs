using System;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Correction;

namespace ProvostXMemory.CorrectionTests;

/// <summary>Test double: rewriter returning a fixed text; records whether it ran and on what input.</summary>
public sealed class RecordingQueryRewriter : IQueryRewriter
{
    private readonly string _rewrittenText;

    public RecordingQueryRewriter(string rewrittenText)
    {
        _rewrittenText = rewrittenText;
    }

    public bool Called { get; private set; }

    public string? LastInput { get; private set; }

    public Task<string> RewriteAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
    {
        Called = true;
        LastInput = query.Text;
        return Task.FromResult(_rewrittenText);
    }
}