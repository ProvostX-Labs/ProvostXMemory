using System;
using System.Collections.Generic;
using System.Linq;

namespace ProvostXMemory.Lexical;

/// <summary>
/// BM25-lite keyword index over documents. Inverted postings over body terms, plus
/// key-term and exact-category maps so key-only and category-only matches surface.
/// Scoring is delegated to <see cref="IBm25Scorer"/>; ordering is fully deterministic
/// (score desc, then document id ascending).
/// </summary>
public sealed class LexicalIndex : ILexicalIndex
{
    private const int DefaultTopK = 10;

    private readonly IBm25Scorer _scorer;
    private readonly Dictionary<string, LexicalDocument> _documents;
    private readonly Dictionary<string, List<LexicalPosting>> _bodyPostings;
    private readonly Dictionary<string, List<string>> _keyPostings;
    private readonly Dictionary<string, List<string>> _categoryDocuments;

    public LexicalIndex(IBm25Scorer scorer)
    {
        _scorer = scorer ?? throw new ArgumentNullException(nameof(scorer));
        _documents = new Dictionary<string, LexicalDocument>(StringComparer.Ordinal);
        _bodyPostings = new Dictionary<string, List<LexicalPosting>>(StringComparer.Ordinal);
        _keyPostings = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        _categoryDocuments = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    }

    public long Count => _documents.Count;

    public CorpusStatistics Statistics { get; private set; } = CorpusStatistics.Empty;

    /// <inheritdoc />
    public void Build(IReadOnlyList<LexicalDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        _documents.Clear();
        _bodyPostings.Clear();
        _keyPostings.Clear();
        _categoryDocuments.Clear();

        foreach (var document in documents)
        {
            ThrowIfDuplicateId(_documents, document);
            IndexDocument(document);
        }

        RefreshStatistics();
    }

    /// <inheritdoc />
    public void IncrementalAdd(LexicalDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        ThrowIfDuplicateId(_documents, document);
        IndexDocument(document);
        RefreshStatistics();
    }

    /// <inheritdoc />
    public bool Remove(string documentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        if (!_documents.ContainsKey(documentId))
        {
            return false;
        }

        var remaining = _documents.Values
            .Where(document => !string.Equals(document.Id, documentId, StringComparison.Ordinal))
            .ToList();
        Build(remaining);
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyList<LexicalDocument> EnumerateDocuments()
    {
        return _documents.Values
            .OrderBy(document => document.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ScoredLexicalId> Search(IReadOnlyList<string> queryTokens, int topK)
    {
        ArgumentNullException.ThrowIfNull(queryTokens);

        var effectiveTopK = topK <= 0 ? DefaultTopK : topK;
        if (queryTokens.Count == 0 || _documents.Count == 0)
        {
            return Array.Empty<ScoredLexicalId>();
        }

        var candidateIds = CollectCandidates(queryTokens);
        if (candidateIds.Count == 0)
        {
            return Array.Empty<ScoredLexicalId>();
        }

        var scored = new List<ScoredLexicalId>(candidateIds.Count);
        foreach (var candidateId in candidateIds)
        {
            var document = _documents[candidateId];
            var score = _scorer.Score(
                statistics: Statistics,
                queryTerms: queryTokens,
                documentTerms: document.BodyTerms,
                keyTerms: document.KeyTerms,
                category: document.Category);
            if (score > 0)
            {
                scored.Add(new ScoredLexicalId(candidateId, score));
            }
        }

        scored.Sort(CompareScored);
        return scored.Take(effectiveTopK).ToList();
    }

    private HashSet<string> CollectCandidates(IReadOnlyList<string> queryTokens)
    {
        var candidateIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var term in queryTokens.Distinct())
        {
            if (_bodyPostings.TryGetValue(term, out var postings))
            {
                foreach (var posting in postings)
                {
                    candidateIds.Add(posting.DocumentId);
                }
            }

            if (_keyPostings.TryGetValue(term, out var keyDocumentIds))
            {
                foreach (var documentId in keyDocumentIds)
                {
                    candidateIds.Add(documentId);
                }
            }
        }

        // The category bonus applies only to single-term queries (exact category match).
        if (queryTokens.Count == 1)
        {
            var categoryKey = queryTokens[0];
            if (_categoryDocuments.TryGetValue(categoryKey, out var categoryDocumentIds))
            {
                foreach (var documentId in categoryDocumentIds)
                {
                    candidateIds.Add(documentId);
                }
            }
        }

        return candidateIds;
    }

    private void IndexDocument(LexicalDocument document)
    {
        _documents.Add(document.Id, document);

        foreach (var term in document.BodyTerms.Distinct())
        {
            if (!_bodyPostings.TryGetValue(term, out var postings))
            {
                postings = new List<LexicalPosting>();
                _bodyPostings.Add(term, postings);
            }

            postings.Add(new LexicalPosting(document.Id, CountTerm(document.BodyTerms, term)));
        }

        foreach (var keyTerm in document.KeyTerms.Distinct())
        {
            if (!_keyPostings.TryGetValue(keyTerm, out var keyDocumentIds))
            {
                keyDocumentIds = new List<string>();
                _keyPostings.Add(keyTerm, keyDocumentIds);
            }

            keyDocumentIds.Add(document.Id);
        }

        if (!string.IsNullOrEmpty(document.Category))
        {
            var categoryKey = document.Category.ToLowerInvariant();
            if (!_categoryDocuments.TryGetValue(categoryKey, out var categoryDocumentIds))
            {
                categoryDocumentIds = new List<string>();
                _categoryDocuments.Add(categoryKey, categoryDocumentIds);
            }

            categoryDocumentIds.Add(document.Id);
        }
    }

    private void RefreshStatistics()
    {
        var terms = new Dictionary<string, TermStat>(StringComparer.Ordinal);
        foreach (var document in _documents.Values)
        {
            // Document frequency counts documents containing the term in body OR key terms.
            var documentTerms = new HashSet<string>(document.BodyTerms, StringComparer.Ordinal);
            foreach (var keyTerm in document.KeyTerms)
            {
                documentTerms.Add(keyTerm);
            }

            var bodyCounts = LexicalTermCounts.Count(document.BodyTerms);
            foreach (var term in documentTerms)
            {
                var existing = terms.TryGetValue(term, out var stat) ? stat : null;
                var documentFrequency = existing is null ? 1 : existing.DocumentFrequency + 1;
                var totalOccurrences = (existing is null ? 0 : existing.TotalOccurrences)
                    + (bodyCounts.TryGetValue(term, out var occurrences) ? occurrences : 0);
                terms[term] = new TermStat(term, documentFrequency, totalOccurrences);
            }
        }

        Statistics = new CorpusStatistics(_documents.Count, terms);
    }

    // Pure term-count within one token list; intentionally static per architecture rule.
    private static int CountTerm(IReadOnlyList<string> terms, string term)
    {
        var count = 0;
        foreach (var candidate in terms)
        {
            if (candidate == term)
            {
                count++;
            }
        }

        return count;
    }

    private static void ThrowIfDuplicateId(
        Dictionary<string, LexicalDocument> documents,
        LexicalDocument document)
    {
        if (documents.ContainsKey(document.Id))
        {
            throw new ArgumentException($"Duplicate document id: {document.Id}", nameof(documents));
        }
    }

    // Pure comparison of two scored hits; intentionally static per architecture rule.
    private static int CompareScored(ScoredLexicalId left, ScoredLexicalId right)
    {
        var byScore = right.Score.CompareTo(left.Score);
        return byScore != 0 ? byScore : string.CompareOrdinal(left.Id, right.Id);
    }
}