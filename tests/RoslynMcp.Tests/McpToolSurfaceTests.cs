using ModelContextProtocol.Client;
using ModelContextProtocol.Server;
using System.Reflection;

namespace RoslynMcp.Tests;

public sealed class McpToolSurfaceTests
{
    private static readonly string[] ExpectedTools =
    [
        "add-bravo-authorize", "add-missing-usings", "add-null-checks", "analyze-control-flow", "analyze-data-flow",
        "change-signature", "convert-expression-body", "convert-foreach-linq", "convert-property",
        "convert-to-async", "convert-to-interpolated-string", "convert-to-pattern-matching", "diagnose",
        "encapsulate-field", "extract-base-class", "extract-constant", "extract-interface", "extract-method",
        "extract-variable", "find-callers", "find-implementations", "find-references", "format-document",
        "generate-constructor", "generate-equals-hashcode", "generate-overrides", "generate-tostring",
        "get-code-metrics", "get-diagnostics", "get-document-outline", "get-symbol-info", "get-type-hierarchy",
        "go-to-definition", "implement-interface", "inline-variable", "introduce-parameter", "move-type-to-file",
        "move-type-to-namespace", "remove-unused-usings", "rename-symbol", "search-symbols", "sort-usings"
    ];

    [Fact]
    public void RoslynTools_ExposeExactlyTheReadmeToolSurface()
    {
        var actual = typeof(RoslynTools).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
            .Where(attribute => attribute is not null)
            .Select(attribute => attribute!.Name!)
            .Order()
            .ToArray();

        Assert.Equal(42, actual.Length);
        Assert.Equal(ExpectedTools.Order(), actual);
    }

    [Fact]
    public async Task StdioServer_AdvertisesAllToolsAndRunsDiagnose()
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "RoslynMcp integration test",
            Command = "dotnet",
            Arguments = [Path.Combine(AppContext.BaseDirectory, "RoslynMcp.dll")],
            WorkingDirectory = GetRepositoryRoot()
        });

        await using var client = await McpClient.CreateAsync(transport);
        var tools = await client.ListToolsAsync();
        Assert.Equal(ExpectedTools.Order(), tools.Select(tool => tool.Name).Order());

        var result = await client.CallToolAsync("diagnose", new Dictionary<string, object?> { ["path"] = "" });
        Assert.NotEqual(true, result.IsError);
        Assert.NotEmpty(result.Content);

        var search = await client.CallToolAsync("search-symbols", new Dictionary<string, object?>
        {
            ["path"] = Path.Combine(GetRepositoryRoot(), "samples", "SkillFixture", "SkillFixture.slnx"),
            ["parameters"] = new Dictionary<string, object?>
            {
                ["query"] = "PaymentService",
                ["kindFilter"] = "Class",
                ["maxResults"] = 10
            }
        });
        Assert.NotEqual(true, search.IsError);
        Assert.Contains(search.Content, content => content.ToString()!.Contains("PaymentService", StringComparison.Ordinal));

        var workbook = BravoAuthorizeExcelReaderTests.CreateWorkbook("Permissions", worksheet =>
        {
            worksheet.Cells[1, 1].Value = "Controller Name";
            worksheet.Cells[1, 2].Value = "Action Name";
            worksheet.Cells[1, 3].Value = "Claims";
            worksheet.Cells[2, 1].Value = "CompanyController";
            worksheet.Cells[2, 2].Value = "SearchAsync";
            worksheet.Cells[2, 3].Value = "Companies_Retrieve; Companies_All";
        });
        try
        {
            var authorize = await client.CallToolAsync("add-bravo-authorize", new Dictionary<string, object?>
            {
                ["path"] = Path.Combine(GetRepositoryRoot(), "samples", "SkillFixture", "SkillFixture.slnx"),
                ["parameters"] = new Dictionary<string, object?>
                {
                    ["excelPath"] = workbook,
                    ["sheetName"] = "Permissions",
                    ["preview"] = true
                }
            });
            Assert.NotEqual(true, authorize.IsError);
            Assert.Contains(authorize.Content, content => content.ToString()!.Contains("Companies_Retrieve", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(workbook);
        }
    }

    private static string GetRepositoryRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
}
