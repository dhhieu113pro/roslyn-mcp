using OfficeOpenXml;
using System.Diagnostics.CodeAnalysis;

namespace RoslynMcp;

[ExcludeFromCodeCoverage(Justification = "EPPlus integration is covered by focused workbook integration tests.")]
public static class BravoAuthorizeExcelReader
{
    private static readonly char[] ClaimSeparators = [';', ',', '\r', '\n'];

    public static IReadOnlyList<BravoAuthorizeMapping> Read(string excelPath, string? sheetName = null)
    {
        if (string.IsNullOrWhiteSpace(excelPath))
            throw new ArgumentException("Excel path cannot be empty.", nameof(excelPath));

        var fullPath = Path.GetFullPath(excelPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Authorization workbook was not found.", fullPath);
        if (!Path.GetExtension(fullPath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Authorization workbook must be an .xlsx file.", nameof(excelPath));

        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var package = new ExcelPackage(stream);
        var worksheet = string.IsNullOrWhiteSpace(sheetName)
            ? package.Workbook.Worksheets.FirstOrDefault()
            : package.Workbook.Worksheets.FirstOrDefault(candidate =>
                candidate.Name.Equals(sheetName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (worksheet is null)
            throw new ArgumentException(string.IsNullOrWhiteSpace(sheetName)
                ? "Authorization workbook does not contain a worksheet."
                : $"Worksheet '{sheetName}' was not found.", nameof(sheetName));
        if (worksheet.Dimension is null)
            throw new ArgumentException($"Worksheet '{worksheet.Name}' is empty.", nameof(excelPath));

        var headers = Enumerable.Range(worksheet.Dimension.Start.Column, worksheet.Dimension.Columns)
            .Select(column => new { Column = column, Name = NormalizeHeader(worksheet.Cells[worksheet.Dimension.Start.Row, column].Text) })
            .Where(header => header.Name.Length > 0)
            .GroupBy(header => header.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Column, StringComparer.OrdinalIgnoreCase);

        var controllerColumn = RequireHeader(headers, "controllername", "Controller Name");
        var actionColumn = RequireHeader(headers, "actionname", "Action Name");
        var claimsColumn = RequireHeader(headers, "claims", "Claims");
        headers.TryGetValue("parametertypes", out var parameterTypesColumn);
        var rows = new List<BravoAuthorizeMapping>();

        for (var row = worksheet.Dimension.Start.Row + 1; row <= worksheet.Dimension.End.Row; row++)
        {
            var controller = worksheet.Cells[row, controllerColumn].Text.Trim();
            var action = worksheet.Cells[row, actionColumn].Text.Trim();
            var claimsText = worksheet.Cells[row, claimsColumn].Text.Trim();
            if (controller.Length == 0 && action.Length == 0 && claimsText.Length == 0)
                continue;
            if (controller.Length == 0 || action.Length == 0 || claimsText.Length == 0)
                throw new ArgumentException($"Excel row {row} must contain Controller Name, Action Name, and Claims.", nameof(excelPath));

            var claims = claimsText.Split(ClaimSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeClaim)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (claims.Length == 0)
                throw new ArgumentException($"Excel row {row} must contain at least one claim.", nameof(excelPath));
            IReadOnlyList<string>? parameterTypes = parameterTypesColumn == 0
                ? null
                : worksheet.Cells[row, parameterTypesColumn].Text
                    .Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            rows.Add(new(row, controller, action, claims, parameterTypes));
        }

        return rows;
    }

    private static string NormalizeHeader(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static int RequireHeader(IReadOnlyDictionary<string, int> headers, string normalized, string displayName) =>
        headers.TryGetValue(normalized, out var column)
            ? column
            : throw new ArgumentException($"Worksheet is missing the required '{displayName}' column.");

    private static string NormalizeClaim(string value)
    {
        var claim = value.Trim();
        return claim.StartsWith("BravoClaimConstants.", StringComparison.Ordinal)
            ? claim
            : $"BravoClaimConstants.{claim}";
    }
}
