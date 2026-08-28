using OfficeOpenXml;

namespace RoslynMcp.Tests;

public sealed class ControllerActionExporterTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private static readonly string Solution = Path.Combine(Root, "tests", "RoslynMcp.Tests", "Fixtures", "SkillFixture", "SkillFixture.slnx");

    [Fact]
    public async Task ExecuteAsync_ExportsReviewWorkbookWithMethodsAndEditableClaims()
    {
        var output = Path.Combine(Path.GetTempPath(), $"actions-{Guid.NewGuid():N}.xlsx");
        try
        {
            var result = await ControllerActionExporter.ExecuteAsync(Solution,
                new() { ExcelPath = output, SheetName = "Review" });

            Assert.Equal(Path.GetFullPath(output), result.ExcelPath);
            Assert.Equal("Review", result.SheetName);
            Assert.Equal(5, result.ExportedActions);
            Assert.Equal(5, result.SkippedAuthorized);
            Assert.Equal(1, result.SkippedAnonymous);
            Assert.All(result.Rows, row => Assert.Equal("CompanyController", row.ControllerName));
            Assert.DoesNotContain(result.Rows, row => row.ActionName is "Helper" or "PublicStatus" or "AlreadyAuthorized");
            Assert.Equal("GET", Assert.Single(result.Rows, row => row.ActionName == "SearchAsync").Method);
            var create = Assert.Single(result.Rows, row => row.ActionName == "CreateAsync");
            Assert.Equal("POST", create.Method);
            Assert.Equal(["string"], create.ParameterTypes);
            Assert.Equal(2, result.Rows.Count(row => row.ActionName == "OverloadedAsync"));

            using (var package = new ExcelPackage(new FileInfo(output)))
            {
                var sheet = package.Workbook.Worksheets["Review"];
                Assert.Equal("Controller Name", sheet.Cells[1, 1].Text);
                Assert.Equal("Action Name", sheet.Cells[1, 2].Text);
                Assert.Equal("Parameter Types", sheet.Cells[1, 3].Text);
                Assert.Equal("Method", sheet.Cells[1, 4].Text);
                Assert.Equal("Claims", sheet.Cells[1, 5].Text);
                Assert.Equal("FF1F4E79", sheet.Cells[1, 1].Style.Fill.BackgroundColor.Rgb);
                Assert.Equal("FFFFF2CC", sheet.Cells[2, 5].Style.Fill.BackgroundColor.Rgb);
                Assert.True(sheet.Column(5).Width >= 55);
                for (var row = 2; row <= sheet.Dimension.End.Row; row++)
                    sheet.Cells[row, 5].Value = "Companies_Retrieve";
                package.Save();
            }

            var preview = await BravoAuthorizeService.ExecuteAsync(Solution,
                new() { AuthorizeAttributeName = "BravoAuthorize", ExcelPath = output, SheetName = "Review" });
            Assert.Equal(5, preview.Rows.Count);
            Assert.All(preview.Rows, row => Assert.Equal("preview", row.Status));
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public async Task ExecuteAsync_CanIncludeAuthorizedActionsAndPrefillClaims()
    {
        var output = Path.Combine(Path.GetTempPath(), $"actions-{Guid.NewGuid():N}.xlsx");
        try
        {
            var result = await ControllerActionExporter.ExecuteAsync(Solution,
                new() { ExcelPath = output, IncludeAuthorized = true });

            Assert.Equal(10, result.ExportedActions);
            var authorized = Assert.Single(result.Rows, row => row.ActionName == "AlreadyAuthorized");
            Assert.Equal(["Companies_Retrieve", "Companies_All"], authorized.Claims);
            var controllerAuthorized = Assert.Single(result.Rows, row => row.ActionName == "Dashboard");
            Assert.Equal(["Companies_All"], controllerAuthorized.Claims);
            var combined = Assert.Single(result.Rows, row => row.ActionName == "Details");
            Assert.Equal(["Companies_All", "Companies_Retrieve"], combined.Claims);
            var positional = Assert.Single(result.Rows, row => row.ActionName == "PositionalAuthorization");
            Assert.Equal(["Companies_Retrieve"], positional.Claims);
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ProtectsExistingWorkbookUnlessOverwriteIsEnabled()
    {
        var output = Path.Combine(Path.GetTempPath(), $"actions-{Guid.NewGuid():N}.xlsx");
        await File.WriteAllTextAsync(output, "existing");
        try
        {
            await Assert.ThrowsAsync<IOException>(() => ControllerActionExporter.ExecuteAsync(
                Solution, new() { ExcelPath = output }));

            var result = await ControllerActionExporter.ExecuteAsync(
                Solution, new() { ExcelPath = output, Overwrite = true });
            Assert.Equal(5, result.ExportedActions);
            using var package = new ExcelPackage(new FileInfo(output));
            Assert.NotNull(package.Workbook.Worksheets["Actions"]);
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public async Task ExecuteAsync_QualifiesDuplicateControllerNamesEvenWhenOneIsExcluded()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"duplicate-controllers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var project = Path.Combine(directory, "Fixture.csproj");
        var output = Path.Combine(directory, "actions.xlsx");
        await File.WriteAllTextAsync(project,
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(directory, "Controllers.cs"), """
            public sealed class AllowAnonymousAttribute : System.Attribute;
            public sealed class ControllerAttribute : System.Attribute;
            public sealed class BravoAuthorizeAttribute(string[] claims) : System.Attribute;
            public static class BravoClaimConstants
            {
                public const string Companies_Retrieve = "Companies_Retrieve";
            }
            namespace A { public sealed class Request; }
            namespace B { public sealed class Request; }
            namespace First
            {
                public sealed class CompanyController
                {
                    public void Search(A.Request request) { }
                    public void Search(B.Request request) { }
                }
            }
            namespace Second { [AllowAnonymous] public sealed class CompanyController { public void Status() { } } }
            namespace Third { [Controller] public sealed class Reports { public void List() { } } }
            """);
        try
        {
            var result = await ControllerActionExporter.ExecuteAsync(project, new() { ExcelPath = output });
            Assert.Equal(3, result.Rows.Count);
            var overloaded = result.Rows.Where(row => row.ActionName == "Search").ToArray();
            Assert.All(overloaded, row => Assert.Equal("First.CompanyController", row.ControllerName));
            Assert.Equal(["A.Request", "B.Request"], overloaded.SelectMany(row => row.ParameterTypes).Order());
            Assert.Equal("Reports", Assert.Single(result.Rows, row => row.ActionName == "List").ControllerName);
            Assert.Equal(1, result.SkippedAnonymous);

            using (var package = new ExcelPackage(new FileInfo(output)))
            {
                var sheet = package.Workbook.Worksheets["Actions"];
                for (var row = 2; row <= sheet.Dimension.End.Row; row++)
                    sheet.Cells[row, 5].Value = "Companies_Retrieve";
                package.Save();
            }
            var preview = await BravoAuthorizeService.ExecuteAsync(project,
                new() { AuthorizeAttributeName = "BravoAuthorize", ExcelPath = output });
            Assert.Equal(3, preview.Rows.Count);
            Assert.All(preview.Rows, row => Assert.Equal("preview", row.Status));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ExportsFromRepositoryDirectoryWithoutWorkspace()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"source-only-controllers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var output = Path.Combine(directory, "actions.xlsx");
        await File.WriteAllTextAsync(Path.Combine(directory, "ReportsController.cs"), """
            public sealed class HttpGetAttribute : System.Attribute;
            public sealed class ReportsController
            {
                [HttpGet]
                public string List(int page) => page.ToString();
            }
            """);
        try
        {
            var result = await ControllerActionExporter.ExecuteAsync(directory, new() { ExcelPath = output });

            var row = Assert.Single(result.Rows);
            Assert.Equal("ReportsController", row.ControllerName);
            Assert.Equal("List", row.ActionName);
            Assert.Equal(["int"], row.ParameterTypes);
            Assert.Equal("GET", row.Method);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
