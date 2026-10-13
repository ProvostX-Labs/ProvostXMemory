using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;

namespace ProvostXMemory.Chunking;


/// <summary>Resolves chunk ids back to their parent record (parent-document recall).</summary>
public interface IParentResolver
{
    Task<MemoryRecord?> ResolveAsync(string chunkId, CancellationToken cancellationToken = default);
}
