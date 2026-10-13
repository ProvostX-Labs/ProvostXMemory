using System.Threading;
using System.Threading.Tasks;

namespace ProvostXMemory.Abstractions;

/// <summary>Manifest read/write for a memory root.</summary>
public interface IMemoryManifestCodec
{
    Task<MemoryManifest> ReadAsync(string rootPath, CancellationToken cancellationToken = default);

    Task WriteAsync(string rootPath, MemoryManifest manifest, CancellationToken cancellationToken = default);
}