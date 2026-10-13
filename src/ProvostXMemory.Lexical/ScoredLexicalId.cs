using System;
using System.Collections.Generic;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Lexical;


/// <summary>Lexical index search hit: id plus BM25-lite score.</summary>
public sealed record ScoredLexicalId(string Id, double Score);

