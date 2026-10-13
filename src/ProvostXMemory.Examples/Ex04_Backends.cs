using System.Diagnostics;
using ProvostXMemory.Abstractions;
using ProvostXMemory.VectorIndex;
using ProvostXMemory.VectorIndex.Engine;
using ProvostXMemory.VectorIndex.Gpu;

namespace ProvostXMemory.Examples;

/// <summary>
/// Backend parity: the same dataset is loaded into four services — engine Flat, IVF,
/// HNSW, and the zero-dependency FlatJson index — plus the GPU backend (with a fallback
/// note: without a GPU the factory transparently runs its CPU kernels). Every backend
/// returns the same hits; timings are printed per backend.
/// </summary>
public sealed class Ex04_Backends : ExampleBase
{
    private static readonly (string Id, string Content)[] Dataset =
    {
        ("kube-1", "kubernetes cluster rollout completed without downtime"),
        ("kube-2", "helm chart release pinned to the stable repository"),
        ("pg-1", "postgres migration added the audit index overnight"),
        ("pg-2", "vacuum reclaimed tablespace after the migration"),
        ("bill-1", "invoice billing refund processed by the payment service"),
        ("bill-2", "chargeback disputed receipt matched the original charge"),
        ("bake-1", "sourdough starter ferment schedule crumb oven"),
        ("bake-2", "nocturne pedal polyrhythm practice session"), // hard negative filler
    };

    public override async Task RunAsync()
    {
        var rig = new ExampleRig();
        var query = "kubernetes rollout";
        var scope = new MemoryScope(MemoryScopeKind.Session, "ops-demo");

        Console.WriteLine($"Dataset: {Dataset.Length} records, query: \"{query}\", top-5 per backend.");
        Console.WriteLine();

        var backends = new (string Name, IndexKind Kind, Func<string, IVectorIndexFactory> Factory)[]
        {
            ("Engine/Flat", IndexKind.Flat, _ => new EngineVectorIndexFactory()),
            ("Engine/IVF", IndexKind.Ivf, _ => rig.IvfFactory()),
            ("Engine/HNSW", IndexKind.Hnsw, _ => rig.HnswFactory()),
            ("FlatJson", IndexKind.Flat, root =>
            {
                var embedder = rig.CreateEmbedder();
                return new ExampleRig.FlatJsonFactory(root, embedder.ModelHash);
            }),
            ("GPU", IndexKind.Gpu, _ => new GpuVectorIndexFactory(new GpuAccelerator(), new EngineVectorIndexFactory())),
        };

        IReadOnlyList<string>? baseline = null;
        foreach (var (name, kind, factoryFor) in backends)
        {
            var root = rig.FreshRoot($"ex04-{kind}");
            var embedder = rig.CreateEmbedder();
            var service = rig.BuildService(
                root,
                embedder,
                indexKind: kind,
                indexFactory: factoryFor(root));

            var scopeSnapshot = scope;
            foreach (var (id, content) in Dataset)
            {
                await service.RememberAsync(new MemoryRecord(id, scopeSnapshot, id, content))
                    .ConfigureAwait(false);
            }

            await service.BuildIndexesAsync(scope).ConfigureAwait(false);

            var watch = Stopwatch.StartNew();
            var result = await service.RecallAsync(new RetrievalQuery(query, scope, TopK: 5))
                .ConfigureAwait(false);
            watch.Stop();

            var ids = result.Records.Select(hit => hit.Record.Id).ToList();
            var parity = baseline is null ? "baseline" : SameSet(baseline, ids) ? "identical" : "DIFFERENT!";
            baseline ??= ids;
            Console.WriteLine($"  {name,-12} {watch.Elapsed.TotalMilliseconds,7:0.0} ms  top-5: [{string.Join(", ", ids)}]  parity: {parity}");

            if (kind == IndexKind.Gpu)
            {
                Console.WriteLine("             (GPU backend: guarded availability — without a GPU the");
                Console.WriteLine("              factory transparently falls back to CPU kernels, identical results)");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Every backend returns the same hits — backend choice is a performance");
        Console.WriteLine("decision, never a semantics decision.");
    }

    private static bool SameSet(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Count == right.Count && left.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(
            right.OrderBy(id => id, StringComparer.Ordinal));
}
