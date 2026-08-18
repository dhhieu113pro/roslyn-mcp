using System.Text.Json;
using RoslynMcp.Contracts.Models;

namespace RoslynMcp.Tests;

public sealed class ExtendedToolServiceTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private static readonly string Solution = Path.Combine(Root, "samples", "SkillFixture", "SkillFixture.slnx");

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
    public async Task ExecuteAsync_RejectsUnknownOperationsAndInvalidParameterTypes()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ExtendedToolService.ExecuteAsync(Solution, "missing", null));
        await Assert.ThrowsAsync<ArgumentException>(() => ExtendedToolService.ExecuteAsync(Solution, "search-symbols", null));
        await Assert.ThrowsAsync<ArgumentException>(() => ExtendedToolService.ExecuteAsync(Solution, "search-symbols", new object()));
    }
}
