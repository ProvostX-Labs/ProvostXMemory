using ProvostXMemory.Abstractions;
using ProvostXMemory.VectorIndex;

namespace ProvostXMemory.ConsolidationTests;

/// <summary>
/// Deterministic hash-based fake embedder: each token hashes into a fixed 512-dim
/// vector slot (FNV-1a mod dim, verified collision-free for the test vocabulary);
/// the result depends only on the text, never on evaluation order or timing.
/// </summary>
public sealed class HashFakeEmbedder : IVectorEmbedder
{
    private const int VectorDim = 512;

    public string ModelHash => "fake-hash-v1";

    public int Dim => VectorDim;

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var vector = new float[Dim];
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var isWord = i < text.Length && char.IsLetterOrDigit(text[i]);
            if (isWord && start < 0)
            {
                start = i;
            }
            else if (!isWord && start >= 0)
            {
                vector[Slot(text[start..i])] += 1f;
                start = -1;
            }
        }

        return Task.FromResult(vector);
    }

    // Pure hash → slot mapping (FNV-1a 64 over UTF-32 code units, mod dim).
    private static int Slot(string token)
    {
        var hash = 0xcbf29ce484222325UL;
        foreach (var ch in token)
        {
            hash ^= ch;
            hash = hash * 0x100000001b3UL;
        }

        return (int)(hash % VectorDim);
    }
}