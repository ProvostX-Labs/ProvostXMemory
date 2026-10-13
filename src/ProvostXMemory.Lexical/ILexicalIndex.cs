using System;
using System.Collections.Generic;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Lexical;


/// <summary>
/// <para>The lexical half of hybrid retrieval: a BM25-weighted inverted index over record
/// text, kept in sync by the memory service (records remember/update → IncrementalAdd;
/// forget → Remove). Complements the dense stage by matching exact terms embeddings miss
/// (identifiers, acronyms, version numbers); both stages fuse into one ranking via
/// <c>IResultFuser</c>.</para>
/// <para><b>Contract:</b> tokens are lowercase alphanumeric words (no stemming, no external
/// NLP); scores are saturated-TF × IDF with a key-match boost. The index itself is not
/// thread-safe — the memory service serializes all mutations; external callers must not
/// share an instance unsynchronized.</para>
/// </summary>
public interface ILexicalIndex
{
    /// <summary>Number of documents in the index.</summary>
    long Count { get; }

    /// <summary>Corpus statistics over all indexed documents (term frequencies + document frequency).</summary>
    CorpusStatistics Statistics { get; }

    /// <summary>Replaces the index contents with <paramref name="documents"/>.</summary>
    void Build(IReadOnlyList<LexicalDocument> documents);

    /// <summary>Adds one document incrementally (duplicate ids throw).</summary>
    void IncrementalAdd(LexicalDocument document);

    /// <summary>Removes one document by id; returns false when the id is unknown.</summary>
    bool Remove(string documentId);

    /// <summary>Enumerates all indexed documents (snapshot in deterministic id order).</summary>
    IReadOnlyList<LexicalDocument> EnumerateDocuments();

    /// <summary>Returns the top-k documents matching the query tokens by BM25-lite score.</summary>
    IReadOnlyList<ScoredLexicalId> Search(IReadOnlyList<string> queryTokens, int topK);
}

