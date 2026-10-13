using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProvostXMemory.VectorIndex;

/// <summary>
/// The host-injected embedding seam. The library never bundles, downloads, or selects
/// embedding models — it consumes vectors from whatever <see cref="IVectorEmbedder"/>
/// the host wires in, and it <b>validates</b> them instead: <see cref="ModelHash"/> and
/// <see cref="Dim"/> are recorded with every vector namespace, and any index whose
/// hash or dimension mismatches the embedder is rejected rather than silently mixing
/// embeddings from different models.
/// </summary>
/// <remarks>
/// <para>Contract: <see cref="EmbedAsync"/> must be deterministic for the same text and
/// return vectors of exactly <see cref="Dim"/> floats. Zero-norm vectors (all-zero)
/// are tolerated — retrieval falls back to lexical scoring for degenerate cases.</para>
/// <para>Typical wiring: an ONNX runtime embedding model (the ProvostX Inference
/// satellite provides one), any remote embedding API, or a deterministic fake in tests.</para>
/// </remarks>
public interface IVectorEmbedder
{
    string ModelHash { get; }

    int Dim { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}