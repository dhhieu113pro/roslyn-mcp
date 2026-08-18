using OfficeOpenXml;

namespace RoslynMcp.Tests;

public sealed class BravoAuthorizeExcelReaderTests
{
    [Fact]
    public void Read_NormalizesHeadersClaimsAndSkipsBlankRows()
    {
        var path = CreateWorkbook("Permissions", worksheet =>
        {
            worksheet.Cells[1, 1].Value = " Controller Name ";
            worksheet.Cells[1, 2].Value = "ACTION NAME";
            worksheet.Cells[1, 3].Value = "Claims";
            worksheet.Cells[2, 1].Value = "CompanyController";
            worksheet.Cells[2, 2].Value = "SearchAsync";
            worksheet.Cells[2, 3].Value = "Companies_Retrieve; BravoClaimConstants.Companies_All\nCompanies_Retrieve";
        });

        try
        {
            var mapping = Assert.Single(BravoAuthorizeExcelReader.Read(path, "permissions"));
            Assert.Equal(2, mapping.ExcelRow);
            Assert.Equal("CompanyController", mapping.ControllerName);
            Assert.Equal("SearchAsync", mapping.ActionName);
            Assert.Equal(["BravoClaimConstants.Companies_Retrieve", "BravoClaimConstants.Companies_All"], mapping.Claims);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsMissingFilesWrongExtensionsSheetsHeadersAndIncompleteRows()
    {
        Assert.Throws<FileNotFoundException>(() => BravoAuthorizeExcelReader.Read(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx")));
        var textFile = Path.GetTempFileName();
        Assert.Throws<ArgumentException>(() => BravoAuthorizeExcelReader.Read(textFile));
        File.Delete(textFile);

        var missingHeader = CreateWorkbook("Data", worksheet =>
        {
            worksheet.Cells[1, 1].Value = "Controller Name";
            worksheet.Cells[1, 2].Value = "Action Name";
        });
        try
        {
            Assert.Throws<ArgumentException>(() => BravoAuthorizeExcelReader.Read(missingHeader, "Missing"));
            Assert.Throws<ArgumentException>(() => BravoAuthorizeExcelReader.Read(missingHeader));
        }
        finally
        {
            File.Delete(missingHeader);
        }

        var incomplete = CreateWorkbook("Data", worksheet =>
        {
            worksheet.Cells[1, 1].Value = "Controller Name";
            worksheet.Cells[1, 2].Value = "Action Name";
            worksheet.Cells[1, 3].Value = "Claims";
            worksheet.Cells[2, 1].Value = "CompanyController";
        });
        try
        {
            Assert.Throws<ArgumentException>(() => BravoAuthorizeExcelReader.Read(incomplete));
        }
        finally
        {
            File.Delete(incomplete);
        }
    }

    [Fact]
    public void Read_ImportsOptionalParameterTypesFromScannerWorkbook()
    {
        var path = CreateWorkbook("Actions", worksheet =>
        {
            worksheet.Cells[1, 1].Value = "Controller Name";
            worksheet.Cells[1, 2].Value = "Action Name";
            worksheet.Cells[1, 3].Value = "Parameter Types";
            worksheet.Cells[1, 4].Value = "Method";
            worksheet.Cells[1, 5].Value = "Claims";
            worksheet.Cells[2, 1].Value = "CompanyController";
            worksheet.Cells[2, 2].Value = "OverloadedAsync";
            worksheet.Cells[2, 3].Value = "int; string";
            worksheet.Cells[2, 4].Value = "POST";
            worksheet.Cells[2, 5].Value = "Companies_Retrieve";
        });

        try
        {
            var mapping = Assert.Single(BravoAuthorizeExcelReader.Read(path));
            Assert.Equal(["int", "string"], mapping.ParameterTypes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    internal static string CreateWorkbook(string sheetName, Action<ExcelWorksheet> populate)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bravo-{Guid.NewGuid():N}.xlsx");
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add(sheetName);
        populate(worksheet);
        package.SaveAs(new FileInfo(path));
        return path;
    }
}
