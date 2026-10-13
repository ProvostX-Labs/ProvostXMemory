using System;
using System.IO;

using ProvostXMemory.Eval;

using Xunit;

namespace ProvostXMemory.EvalTests;

/// <summary>Default factory wiring: host-agnostic construction of a real service on a temp root.</summary>
public sealed class DefaultEvalServiceFactoryTests
{
    [Fact]
    public void Factory_builds_a_service_bound_to_the_requested_root()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "provostx-eval-factory-test", Guid.NewGuid().ToString("N"));
        try
        {
            var factory = new DefaultEvalServiceFactory(vectorDim: 128);
            var service = factory.Create(rootPath);

            Assert.NotNull(service);
            Assert.Empty(service.Scopes());
        }
        finally
        {
            try
            {
                Directory.Delete(Path.Combine(Path.GetTempPath(), "provostx-eval-factory-test"), recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void Factory_rejects_empty_root()
    {
        var factory = new DefaultEvalServiceFactory();
        Assert.ThrowsAny<ArgumentException>(() => factory.Create(""));
        Assert.ThrowsAny<ArgumentException>(() => factory.Create("  "));
    }
}
