using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.VectorIndex;


/// <summary>A vector index item: id + vector.</summary>
public sealed record VectorItem(string Id, IReadOnlyList<float> Vector);
