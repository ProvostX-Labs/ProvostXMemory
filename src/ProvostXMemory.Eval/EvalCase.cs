using System;
using System.Collections.Generic;
using System.Linq;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Eval;

/// <summary>
/// One golden retrieval case: a query bound to a scope plus the record ids a good
/// retriever should surface. Relevance is binary (1.0) unless graded weights are given.
/// The query's TopK is overridden by the eval runner options' TopK at run time.
/// </summary>
public sealed class EvalCase
{
    private static readonly IReadOnlyList<double> DefaultGrades = Array.Empty<double>();

    private readonly IReadOnlyList<string> _relevantIds;
    private readonly IReadOnlyList<double> _grades;

    public EvalCase(
        string id,
        string query,
        MemoryScope queryScope,
        IReadOnlyList<string> relevantIds,
        IReadOnlyList<double>? gradedRelevance = null)
    {
        ArgumentNullException.ThrowIfNull(relevantIds);
        Validate(id, query, relevantIds);

        _relevantIds = relevantIds.ToList();
        _grades = gradedRelevance is null ? DefaultGrades : gradedRelevance.ToList();
        ValidateGrades(_relevantIds.Count, _grades);

        Id = id;
        Query = query;
        QueryScope = queryScope;
    }

    public string Id { get; }

    public string Query { get; }

    public MemoryScope QueryScope { get; }

    public IReadOnlyList<string> RelevantIds => _relevantIds;

    /// <summary>Per-relevance-judge graded relevance (0..1); empty when binary.</summary>
    public IReadOnlyList<double> GradedRelevance => _grades;

    /// <summary>Relevance of <paramref name="recordId"/> within this case (0 when unjudged).</summary>
    public double RelevanceOf(string recordId)
    {
        for (var i = 0; i < _relevantIds.Count; i++)
        {
            if (string.Equals(_relevantIds[i], recordId, StringComparison.Ordinal))
            {
                return _grades.Count == 0 ? 1.0 : _grades[i];
            }
        }

        return 0.0;
    }

    // Pure validation; intentionally static per architecture rule.
    private static void Validate(string id, string query, IReadOnlyList<string> relevantIds)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Eval case id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Eval query text is required.", nameof(query));
        }

        if (relevantIds.Count == 0)
        {
            throw new ArgumentException("At least one relevant record id is required.", nameof(relevantIds));
        }

        for (var i = 0; i < relevantIds.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(relevantIds[i]))
            {
                throw new ArgumentException($"Relevant id {i} is empty.", nameof(relevantIds));
            }
        }
    }

    // Pure validation; intentionally static per architecture rule.
    private static void ValidateGrades(int relevanceCount, IReadOnlyList<double> grades)
    {
        if (grades.Count == 0)
        {
            return;
        }

        if (grades.Count != relevanceCount)
        {
            throw new ArgumentException(
                $"Graded relevance count {grades.Count} must match relevant id count {relevanceCount}.");
        }

        for (var i = 0; i < grades.Count; i++)
        {
            if (grades[i] is < 0.0 or > 1.0)
            {
                throw new ArgumentException($"Grade {i} must be within [0, 1].");
            }
        }
    }
}