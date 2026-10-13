using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.VectorIndex;


/// <summary>Vector index search hit.</summary>
public sealed record ScoredVectorId(string Id, float Score);

