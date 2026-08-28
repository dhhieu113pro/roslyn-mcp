namespace RoslynMcp.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class McpServerLoggingCollection
{
    public const string Name = "McpServerLogging";
}

[Collection(McpServerLoggingCollection.Name)]
public sealed class McpServerLoggingTests
{
    [Fact]
    public async Task RunAsync_WritesStartupLogToFileWithoutConsoleOutput()
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        foreach (var file in Directory.EnumerateFiles(logDirectory, "roslyn-mcp-*.log"))
            File.Delete(file);

        var originalOut = Console.Out;
        using var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            try
            {
                await McpServerHost.RunAsync([], cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }

            var logFile = Directory.EnumerateFiles(logDirectory, "roslyn-mcp-*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            Assert.NotNull(logFile);
            Assert.Contains("RoslynMcp", await File.ReadAllTextAsync(logFile));
            Assert.Equal(string.Empty, output.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
