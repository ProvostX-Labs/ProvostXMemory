using System;
using System.Collections.Generic;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Lexical;


/// <summary>One body posting: (document id, term frequency).</summary>
public sealed record LexicalPosting(string DocumentId, int TermFrequency);
