using System.Text.Json;
using RoslynMcp.Contracts.Models;

namespace RoslynMcp.Tests;

public sealed class ExtendedToolServiceTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private static readonly string Solution = Path.Combine(Root, "tests", "RoslynMcp.Tests", "Fixtures", "SkillFixture", "SkillFixture.slnx");

    [Fact]
    public async Task ExecuteAsync_RunsTypedQueriesAndDiagnostics()
    {
        Assert.Equal(41, ExtendedToolService.GetOperationNames().Count);
        var parameters = JsonSerializer.Deserialize<SearchSymbolsParams>("{\"query\":\"PaymentService\",\"kindFilter\":\"Class\",\"maxResults\":10}", new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var search = JsonSerializer.Serialize(await ExtendedToolService.ExecuteAsync(Solution, "search-symbols", parameters));
        var diagnosedWorkspace = JsonSerializer.Serialize(await ExtendedToolService.ExecuteAsync(Solution, "diagnose", null));
        var diagnosedEnvironment = JsonSerializer.Serialize(await ExtendedToolService.ExecuteAsync("", "DIAGNOSE", null));

        Assert.Contains("PaymentService", search);
        Assert.Contains("\"healthy\"", diagnosedWorkspace, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"workspace\":null", diagnosedEnvironment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_DiagnoseReportsWorkspaceLoadFailure()
    {
        var missingSolution = Path.Combine(Path.GetTempPath(), $"roslyn-mcp-{Guid.NewGuid():N}.slnx");

        var result = JsonSerializer.SerializeToElement(
            await ExtendedToolService.ExecuteAsync(missingSolution, "diagnose", null));

        Assert.False(result.GetProperty("healthy").GetBoolean());
        Assert.False(result.GetProperty("workspace").GetProperty("loaded").GetBoolean());
        Assert.Equal(missingSolution, result.GetProperty("workspace").GetProperty("path").GetString());
        Assert.Equal("2001", result.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("File not found", result.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnknownOperationsAndInvalidParameterTypes()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ExtendedToolService.ExecuteAsync(Solution, "missing", null));
        await Assert.ThrowsAsync<ArgumentException>(() => ExtendedToolService.ExecuteAsync(Solution, "search-symbols", null));
        await Assert.ThrowsAsync<ArgumentException>(() => ExtendedToolService.ExecuteAsync(Solution, "search-symbols", new object()));
    }
}
