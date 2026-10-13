using System;

using ProvostXMemory.Eval;
using ProvostXMemory.Consolidation;
using ProvostXMemory.Correction;
using ProvostXMemory.Fusion;
using ProvostXMemory.Graph;
using ProvostXMemory.Storage;
using ProvostXMemory.Telemetry;
using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;
using ProvostXMemory.Abstractions;

namespace ProvostXMemory;

/// <summary>
/// Default eval wiring: a real <see cref="MemoryService"/> over the transactional JSONL
/// store + own-engine flat index + RRF, powered by the deterministic
/// <see cref="ClusterHashEmbedder"/> (no real model anywhere). Any host can substitute its
/// own <see cref="EvalServiceFactory"/> to eval its production wiring instead.
/// </summary>
public sealed class DefaultEvalServiceFactory : EvalServiceFactory
{
    private readonly int _vectorDim;

    public DefaultEvalServiceFactory(int vectorDim = 128)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(vectorDim, 16);
        _vectorDim = vectorDim;
    }

    public IMemoryService Create(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var embedder = new ClusterHashEmbedder(_vectorDim);
        var layout = new MemoryRootLayout(rootPath, embedder.ModelHash);
        var builder = new MemoryServiceBuilder()
            .WithOptions(new MemoryServiceOptions(
                Storage: new StorageOptions(rootPath),
                Vector: new VectorOptions(embedder.ModelHash, embedder.Dim),
                Graph: GraphOptions.Default,
                Fusion: new FusionOptions(),
                Consolidation: new ConsolidationOptions()))
            .WithStore(new JsonlMemoryStore(layout))
            .WithEmbedder(embedder)
            .WithIndexFactory(new EngineVectorIndexFactory())
            .WithFuser(new ReciprocalRankFuser())
            .WithTelemetry(NoopMemoryTelemetry.Instance)
            // Scope-pinned graph stores: the same production wiring hosts use, so eval runs
            // exercise the graph ledger and cluster write path end-to-end.
            .WithGraphExtractor(new HeuristicEntityExtractor())
            .WithGraphStoreResolver(scope => new JsonlMemoryGraphStore(layout, scope));
        return builder.Build();
    }
}