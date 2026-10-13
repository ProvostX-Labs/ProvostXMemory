using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Correction;

namespace ProvostXMemory.CorrectionTests;

/// <summary>Test double: grades candidates by record id (default grade for unknown ids); records calls.</summary>
public sealed class StubRetrievalGrader : IRetrievalGrader
{
    private readonly IReadOnlyDictionary<string, RetrievalGrade> _grades;
    private readonly RetrievalGrade _defaultGrade;

    public StubRetrievalGrader(IReadOnlyDictionary<string, RetrievalGrade> grades, RetrievalGrade defaultGrade = RetrievalGrade.Relevant)
    {
        _grades = grades;
        _defaultGrade = defaultGrade;
    }

    public int Calls { get; private set; }

    public Task<GradedCandidate> GradeAsync(RetrievalQuery query, ScoredMemoryRecord candidate, CancellationToken cancellationToken = default)
    {
        Calls++;
        var grade = _grades.TryGetValue(candidate.Record.Id, out var specific) ? specific : _defaultGrade;
        return Task.FromResult(new GradedCandidate(candidate, grade));
    }
}