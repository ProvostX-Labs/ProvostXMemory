using System.Threading.Tasks;

namespace ProvostXMemory.Examples;

/// <summary>Base class for every example: one instance, one Run, no static state.</summary>
public abstract class ExampleBase
{
    /// <summary>Runs the example end-to-end and prints human-readable output.</summary>
    public abstract Task RunAsync();
}
