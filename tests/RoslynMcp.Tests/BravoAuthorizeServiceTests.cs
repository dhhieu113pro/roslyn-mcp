namespace RoslynMcp.Tests;

public sealed class BravoAuthorizeServiceTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private static readonly string Solution = Path.Combine(Root, "samples", "SkillFixture", "SkillFixture.slnx");

    [Fact]
    public async Task ExecuteAsync_PreviewsFormattedDirectClaimConstants()
    {
        var workbook = CreateMapping(("Company", "SearchAsync", "Companies_Retrieve; Companies_All"));
        try
        {
            var result = await BravoAuthorizeService.ExecuteAsync(Solution,
                new() { ExcelPath = workbook, SheetName = "Permissions" });

            var row = Assert.Single(result.Rows);
            Assert.True(result.Preview);
            Assert.False(result.Applied);
            Assert.Equal("preview", row.Status);
            Assert.Contains("BravoClaimConstants.Companies_Retrieve", row.GeneratedAttribute);
            Assert.DoesNotContain("nameof", row.GeneratedAttribute);
            Assert.DoesNotContain("roles", row.GeneratedAttribute);
            Assert.DoesNotContain("module", row.GeneratedAttribute);
            Assert.Equal(1, result.Summary.Changed);
        }
        finally
        {
            File.Delete(workbook);
        }
    }

    [Theory]
    [InlineData("MissingController", "SearchAsync", "controller-not-found")]
    [InlineData("CompanyController", "MissingAsync", "action-not-found")]
    [InlineData("CompanyController", "OverloadedAsync", "ambiguous-action")]
    [InlineData("CompanyController", "Helper", "action-not-found")]
    [InlineData("CompanyController", "SearchAsync", "invalid-claim", "Missing_Claim")]
    public async Task ExecuteAsync_ReportsSemanticValidationFailures(
        string controller, string action, string expectedStatus, string claim = "Companies_Retrieve")
    {
        var workbook = CreateMapping((controller, action, claim));
        try
        {
            var result = await BravoAuthorizeService.ExecuteAsync(Solution, new() { ExcelPath = workbook });
            Assert.Equal(expectedStatus, Assert.Single(result.Rows).Status);
            Assert.False(result.Applied);
        }
        finally
        {
            File.Delete(workbook);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RejectsDuplicateRows()
    {
        var workbook = CreateMapping(
            ("Company", "SearchAsync", "Companies_Retrieve"),
            ("CompanyController", "SearchAsync", "Companies_All"));
        try
        {
            var result = await BravoAuthorizeService.ExecuteAsync(Solution, new() { ExcelPath = workbook });
            Assert.All(result.Rows, row => Assert.Equal("duplicate-row", row.Status));
            Assert.Equal(2, result.Summary.Conflicts);
        }
        finally
        {
            File.Delete(workbook);
        }
    }

    [Fact]
    public async Task ExecuteAsync_UsesParameterTypesToResolveOverloadedActions()
    {
        var workbook = CreateMappingWithParameters(
            ("CompanyController", "OverloadedAsync", "", "Companies_Retrieve"),
            ("CompanyController", "OverloadedAsync", "int", "Companies_All"));
        try
        {
            var result = await BravoAuthorizeService.ExecuteAsync(Solution, new() { ExcelPath = workbook });
            Assert.Equal(2, result.Rows.Count);
            Assert.All(result.Rows, row => Assert.Equal("preview", row.Status));
            Assert.Equal(2, result.Summary.Changed);
        }
        finally
        {
            File.Delete(workbook);
        }
    }

    [Theory]
    [InlineData("AlreadyAuthorized", "Companies_All; Companies_Retrieve", "unchanged")]
    [InlineData("ConflictingAuthorization", "Companies_All", "conflict")]
    public async Task ExecuteAsync_HandlesExistingAttributes(
        string action, string claims, string expectedStatus)
    {
        var workbook = CreateMapping(("CompanyController", action, claims));
        try
        {
            var result = await BravoAuthorizeService.ExecuteAsync(Solution, new() { ExcelPath = workbook });
            Assert.Equal(expectedStatus, Assert.Single(result.Rows).Status);
        }
        finally
        {
            File.Delete(workbook);
        }
    }

    [Theory]
    [InlineData("SecuredController", "Dashboard", "Companies_All", "unchanged")]
    [InlineData("SecuredController", "Dashboard", "Companies_Retrieve", "conflict")]
    [InlineData("SecuredController", "Details", "Companies_All; Companies_Retrieve", "unchanged")]
    [InlineData("SecuredController", "Details", "Companies_Retrieve", "conflict")]
    [InlineData("CompanyController", "PositionalAuthorization", "Companies_Retrieve", "unchanged")]
    public async Task ExecuteAsync_HandlesControllerAndPositionalAuthorization(
        string controller, string action, string claims, string expectedStatus)
    {
        var workbook = CreateMapping((controller, action, claims));
        try
        {
            var result = await BravoAuthorizeService.ExecuteAsync(Solution, new() { ExcelPath = workbook });
            Assert.Equal(expectedStatus, Assert.Single(result.Rows).Status);
        }
        finally
        {
            File.Delete(workbook);
        }
    }

    [Fact]
    public async Task ExecuteAsync_AppliesBeautifulFormattingAndIsIdempotent()
    {
        var directory = CreateProject();
        var project = Path.Combine(directory, "Fixture.csproj");
        var source = Path.Combine(directory, "CompanyController.cs");
        var workbook = CreateMapping(("Company", "SearchAsync", "Companies_Retrieve,Companies_All"));
        try
        {
            var applied = await BravoAuthorizeService.ExecuteAsync(project,
                new() { ExcelPath = workbook, Preview = false });

            Assert.True(applied.Applied);
            Assert.Equal("applied", Assert.Single(applied.Rows).Status);
            Assert.Contains(source, applied.ChangedFiles, StringComparer.OrdinalIgnoreCase);
            var code = await File.ReadAllTextAsync(source);
            var expected = "    [HttpGet]\n" +
                "    [BravoAuthorize(\n" +
                "        claims:\n" +
                "        [\n" +
                "            BravoClaimConstants.Companies_Retrieve,\n" +
                "            BravoClaimConstants.Companies_All\n" +
                "        ])]\n" +
                "    public Task<string> SearchAsync()";
            Assert.True(code.Replace("\r\n", "\n", StringComparison.Ordinal).Contains(expected, StringComparison.Ordinal), code);
            Assert.DoesNotContain("nameof", code);
            Assert.DoesNotContain("roles:", code);
            Assert.DoesNotContain("module:", code);

            var secondRun = await BravoAuthorizeService.ExecuteAsync(project,
                new() { ExcelPath = workbook, Preview = false });
            Assert.False(secondRun.Applied);
            Assert.Equal("unchanged", Assert.Single(secondRun.Rows).Status);
        }
        finally
        {
            File.Delete(workbook);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotApplyValidRowsWhenAnyRowFails()
    {
        var directory = CreateProject();
        var project = Path.Combine(directory, "Fixture.csproj");
        var source = Path.Combine(directory, "CompanyController.cs");
        var before = await File.ReadAllTextAsync(source);
        var workbook = CreateMapping(
            ("Company", "SearchAsync", "Companies_Retrieve"),
            ("Missing", "SearchAsync", "Companies_All"));
        try
        {
            var result = await BravoAuthorizeService.ExecuteAsync(project,
                new() { ExcelPath = workbook, Preview = false });
            Assert.False(result.Applied);
            Assert.Empty(result.ChangedFiles);
            Assert.Equal(before, await File.ReadAllTextAsync(source));
        }
        finally
        {
            File.Delete(workbook);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateProject()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bravo-project-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Fixture.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>preview</LangVersion></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(directory, "CompanyController.cs"), """
            public abstract class ControllerBase;
            public sealed class HttpGetAttribute : System.Attribute;
            public sealed class BravoAuthorizeAttribute(string[] claims) : System.Attribute;
            public static class BravoClaimConstants
            {
                public const string Companies_Retrieve = "Companies_Retrieve";
                public const string Companies_All = "Companies_All";
            }
            public sealed class CompanyController : ControllerBase
            {
                [HttpGet]
                public Task<string> SearchAsync() => Task.FromResult("ok");
            }
            """);
        return directory;
    }

    private static string CreateMapping(params (string Controller, string Action, string Claims)[] rows) =>
        BravoAuthorizeExcelReaderTests.CreateWorkbook("Permissions", worksheet =>
        {
            worksheet.Cells[1, 1].Value = "Controller Name";
            worksheet.Cells[1, 2].Value = "Action Name";
            worksheet.Cells[1, 3].Value = "Claims";
            for (var index = 0; index < rows.Length; index++)
            {
                worksheet.Cells[index + 2, 1].Value = rows[index].Controller;
                worksheet.Cells[index + 2, 2].Value = rows[index].Action;
                worksheet.Cells[index + 2, 3].Value = rows[index].Claims;
            }
        });

    private static string CreateMappingWithParameters(
        params (string Controller, string Action, string ParameterTypes, string Claims)[] rows) =>
        BravoAuthorizeExcelReaderTests.CreateWorkbook("Actions", worksheet =>
        {
            worksheet.Cells[1, 1].Value = "Controller Name";
            worksheet.Cells[1, 2].Value = "Action Name";
            worksheet.Cells[1, 3].Value = "Parameter Types";
            worksheet.Cells[1, 4].Value = "Method";
            worksheet.Cells[1, 5].Value = "Claims";
            for (var index = 0; index < rows.Length; index++)
            {
                worksheet.Cells[index + 2, 1].Value = rows[index].Controller;
                worksheet.Cells[index + 2, 2].Value = rows[index].Action;
                worksheet.Cells[index + 2, 3].Value = rows[index].ParameterTypes;
                worksheet.Cells[index + 2, 4].Value = "GET";
                worksheet.Cells[index + 2, 5].Value = rows[index].Claims;
            }
        });
}
