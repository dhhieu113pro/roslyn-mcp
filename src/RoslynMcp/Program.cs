using System.Diagnostics.CodeAnalysis;

namespace RoslynMcp;

[ExcludeFromCodeCoverage(Justification = "Process entry point delegates directly to the tested application boundary.")]
public static class Program
{
    public static Task Main(string[] args) => McpServerHost.RunAsync(args);
}
