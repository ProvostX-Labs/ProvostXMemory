namespace ProvostXMemory.Examples;

/// <summary>
/// Entry point for the runnable example set. With no arguments every example runs in
/// sequence and the process exits 0; pass an example name (e.g. "Ex03_HybridSearch")
/// to run exactly one. Works with no GPU and no network: the embedder is deterministic,
/// the backends are the own engine (CPU) or FlatJson, and storage is a temp directory.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // The catalog: one instance-constructed example per entry, no static state anywhere.
        var catalog = new List<(string Name, Func<Examples.ExampleBase> Create)>
        {
            ("Ex01_Quickstart", () => new Examples.Ex01_Quickstart()),
            ("Ex02_Scopes", () => new Examples.Ex02_Scopes()),
            ("Ex03_HybridSearch", () => new Examples.Ex03_HybridSearch()),
            ("Ex04_Backends", () => new Examples.Ex04_Backends()),
            ("Ex05_Consolidation", () => new Examples.Ex05_Consolidation()),
            ("Ex06_GraphRetrieval", () => new Examples.Ex06_GraphRetrieval()),
            ("Ex07_CorrectiveLoop", () => new Examples.Ex07_CorrectiveLoop()),
            ("Ex08_LoggingAudit", () => new Examples.Ex08_LoggingAudit()),
            ("Ex09_EvalHarness", () => new Examples.Ex09_EvalHarness()),
            ("Ex10_RestartPersistence", () => new Examples.Ex10_RestartPersistence()),
        };

        Console.WriteLine("ProvostXMemory examples");
        Console.WriteLine("=======================");
        foreach (var (name, _) in catalog)
        {
            Console.WriteLine($"  {name}");
        }

        Console.WriteLine();

        var selected = catalog;
        if (args.Length > 0)
        {
            var wanted = args[0];
            selected = catalog.Where(entry =>
                    entry.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (selected.Count == 0)
            {
                Console.WriteLine($"Unknown example '{wanted}'.");
                return 1;
            }
        }

        var failures = 0;
        foreach (var (name, create) in selected)
        {
            Console.WriteLine($"--- {name} " + new string('-', Math.Max(1, 60 - name.Length)));
            try
            {
                await create().RunAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures++;
                Console.WriteLine($"FAILED: {name}: {exception.Message}");
                Console.WriteLine(exception);
            }

            Console.WriteLine();
        }

        Console.WriteLine(failures == 0 ? "All examples completed successfully." : $"{failures} example(s) failed.");
        return failures == 0 ? 0 : 1;
    }
}
