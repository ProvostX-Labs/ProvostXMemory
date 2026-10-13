# ProvostXMemory.Lexical.API.md

Types: 9  |  LOC: 404  |  ~492 tokens

---

Dependencies: ProvostXMemory.Abstractions

### Interface: IBm25Scorer
> Lexical index search hit: id plus BM25-lite score.
Methods:
  - double Score(CorpusStatistics statistics,
        IReadOnlyList<string> queryTerms,
        IReadOnlyList<string> documentTerms,
        IReadOnlyList<string>? keyTerms = null,
        string? category = null,
        double keyBoost = 2.0)
Cross-package deps: ProvostXMemory.Abstractions

### Interface: ILexicalIndex
> Lexical index search hit: id plus BM25-lite score.
Properties:
  - long Count { get; set; }
  - CorpusStatistics Statistics { get; set; }
Methods:
  - void Build(IReadOnlyList<LexicalDocument> documents)
  - void IncrementalAdd(LexicalDocument document)
  - bool Remove(string documentId)
  - IReadOnlyList<LexicalDocument> EnumerateDocuments()
  - IReadOnlyList<ScoredLexicalId> Search(IReadOnlyList<string> queryTokens, int topK)
Cross-package deps: ProvostXMemory.Abstractions

### Class: Bm25Scorer
> BM25-lite scoring: saturated term frequency (k1 = 1.2) × IDF plus a key-match boost
Implements: IBm25Scorer

### Class: LexicalIndex
> BM25-lite keyword index over documents. Inverted postings over body terms, plus
Implements: ILexicalIndex
Constructor:
  - LexicalIndex(IBm25Scorer scorer)

### Class: LexicalTermCounts
> Term-frequency / token-set helpers over pre-tokenized term lists.

### Class: LexicalTokenizer
> Lowercases text and extracts contiguous [a0-9] word tokens (letters, digits, underscore).

### Record: LexicalPosting
> Lexical index search hit: id plus BM25-lite score.
Constructor:
  - LexicalPosting(string DocumentId, int TermFrequency)
Cross-package deps: ProvostXMemory.Abstractions

### Record: ScoredLexicalId
> Lexical index search hit: id plus BM25-lite score.
Constructor:
  - ScoredLexicalId(string Id, double Score)
Cross-package deps: ProvostXMemory.Abstractions

### Record: TermStat
> Immutable per-term corpus statistics for BM25 scoring.
Constructor:
  - TermStat(string Term, int DocumentFrequency, int TotalOccurrences)
