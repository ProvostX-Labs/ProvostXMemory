using System;
using System.Collections.Generic;

namespace ProvostXMemory.Lexical;


/// <summary>Immutable per-term corpus statistics for BM25 scoring.</summary>
public sealed record TermStat(string Term, int DocumentFrequency, int TotalOccurrences);

