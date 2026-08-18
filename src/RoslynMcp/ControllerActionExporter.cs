using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using RoslynMcp.Core.Workspace;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace RoslynMcp;

[ExcludeFromCodeCoverage(Justification = "Roslyn and EPPlus integration is covered by end-to-end fixture tests.")]
public static class ControllerActionExporter
{
    private const string BravoAuthorizeName = "BravoAuthorize";
    private const string AllowAnonymousName = "AllowAnonymous";
    private static readonly SymbolDisplayFormat ParameterTypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                              SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                              SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static async Task<ExportControllerActionsResult> ExecuteAsync(
        string path,
        ExportControllerActionsParams parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var outputPath = ValidateOutputPath(parameters.ExcelPath, parameters.Overwrite);
        var sheetName = string.IsNullOrWhiteSpace(parameters.SheetName) ? "Actions" : parameters.SheetName.Trim();
        var provider = new MSBuildWorkspaceProvider();
        using var context = await provider.CreateContextAsync(path, cancellationToken);
        var rows = new List<ControllerActionExportRow>();
        var skippedAuthorized = 0;
        var skippedAnonymous = 0;
        var seenControllers = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var controllerNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var project in context.Solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
            foreach (var document in project.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var root = await document.GetSyntaxRootAsync(cancellationToken);
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                if (root is null || semanticModel is null)
                    continue;

                foreach (var declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    var controller = semanticModel.GetDeclaredSymbol(declaration, cancellationToken) as INamedTypeSymbol;
                    if (controller is null || !seenControllers.Add(controller) || !IsController(controller))
                        continue;
                    controllerNames.Add(controller.ToDisplayString());

                    var controllerIsAnonymous = HasAttribute(controller, AllowAnonymousName);
                    var controllerIsAuthorized = HasAttribute(controller, BravoAuthorizeName);
                    var controllerClaims = controllerIsAuthorized && parameters.IncludeAuthorized
                        ? await ReadControllerClaimsAsync(controller, cancellationToken)
                        : [];
                    foreach (var action in controller.GetMembers().OfType<IMethodSymbol>().Where(IsControllerAction))
                    {
                        if (controllerIsAnonymous || HasAttribute(action, AllowAnonymousName))
                        {
                            skippedAnonymous++;
                            continue;
                        }

                        var actionIsAuthorized = HasAttribute(action, BravoAuthorizeName);
                        var authorized = controllerIsAuthorized || actionIsAuthorized;
                        if (authorized && !parameters.IncludeAuthorized)
                        {
                            skippedAuthorized++;
                            continue;
                        }

                        var methodSyntax = (MethodDeclarationSyntax)await action.DeclaringSyntaxReferences[0]
                            .GetSyntaxAsync(cancellationToken);
                        var sourceDocument = context.Solution.GetDocument(methodSyntax.SyntaxTree)!;
                        var span = methodSyntax.GetLocation().GetLineSpan();
                        rows.Add(new(
                            controller.ToDisplayString(),
                            action.Name,
                            GetParameterTypes(action),
                            GetHttpMethod(action),
                            actionIsAuthorized
                                ? controllerClaims.Concat(ReadClaims(methodSyntax.AttributeLists)).Distinct(StringComparer.Ordinal).ToArray()
                                : controllerClaims,
                            sourceDocument.FilePath!,
                            span.StartLinePosition.Line + 1));
                    }
                }
            }

        var duplicateShortNames = controllerNames.GroupBy(GetShortControllerName, StringComparer.Ordinal)
            .Where(group => group.Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var displayRows = rows.Select(row =>
        {
            var shortName = GetShortControllerName(row.ControllerName);
            return duplicateShortNames.Contains(shortName) ? row : row with { ControllerName = shortName };
        });
        var orderedRows = displayRows.OrderBy(row => row.ControllerName, StringComparer.Ordinal)
            .ThenBy(row => row.ActionName, StringComparer.Ordinal)
            .ThenBy(row => string.Join(";", row.ParameterTypes), StringComparer.Ordinal)
            .ToArray();
        WriteWorkbook(outputPath, sheetName, orderedRows, parameters.Overwrite);
        return new(outputPath, sheetName, orderedRows.Length, skippedAuthorized, skippedAnonymous, orderedRows);
    }

    internal static IReadOnlyList<string> GetParameterTypes(IMethodSymbol method) =>
        method.Parameters.Select(parameter =>
        {
            var prefix = parameter.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                _ => string.Empty
            };
            return prefix + parameter.Type.ToDisplayString(ParameterTypeFormat);
        }).ToArray();

    internal static bool ParameterTypesMatch(IMethodSymbol method, IReadOnlyList<string> expected) =>
        GetParameterTypes(method).Select(NormalizeType).SequenceEqual(expected.Select(NormalizeType), StringComparer.Ordinal);

    private static string ValidateOutputPath(string excelPath, bool overwrite)
    {
        if (string.IsNullOrWhiteSpace(excelPath))
            throw new ArgumentException("Excel path cannot be empty.", nameof(excelPath));
        var fullPath = Path.GetFullPath(excelPath);
        if (!Path.GetExtension(fullPath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Controller action workbook must be an .xlsx file.", nameof(excelPath));
        if (File.Exists(fullPath) && !overwrite)
            throw new IOException($"Workbook '{fullPath}' already exists. Set overwrite to true to replace it.");
        return fullPath;
    }

    private static bool IsController(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Class &&
        !type.IsAbstract &&
        type.DeclaredAccessibility == Accessibility.Public &&
        !type.IsGenericType &&
        (type.Name.EndsWith("Controller", StringComparison.Ordinal) || HasAttribute(type, "Controller")) &&
        !HasAttribute(type, "NonController");

    private static bool IsControllerAction(IMethodSymbol method) =>
        method.MethodKind == MethodKind.Ordinary &&
        method.DeclaredAccessibility == Accessibility.Public &&
        !method.IsStatic &&
        !method.IsAbstract &&
        !method.IsGenericMethod &&
        !method.IsImplicitlyDeclared &&
        method.DeclaringSyntaxReferences.Length == 1 &&
        !HasAttribute(method, "NonAction");

    private static bool HasAttribute(ISymbol symbol, string shortName) =>
        symbol.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.Name is { } name &&
            (name.Equals(shortName, StringComparison.Ordinal) ||
             name.Equals(shortName + "Attribute", StringComparison.Ordinal)));

    private static string GetHttpMethod(IMethodSymbol method)
    {
        var verbs = new List<string>();
        if (HasAttribute(method, "HttpGet")) verbs.Add("GET");
        if (HasAttribute(method, "HttpPost")) verbs.Add("POST");
        return string.Join(", ", verbs);
    }

    private static async Task<IReadOnlyList<string>> ReadControllerClaimsAsync(
        INamedTypeSymbol controller,
        CancellationToken cancellationToken)
    {
        var claims = new List<string>();
        foreach (var reference in controller.DeclaringSyntaxReferences)
        {
            if (await reference.GetSyntaxAsync(cancellationToken) is TypeDeclarationSyntax declaration)
                claims.AddRange(ReadClaims(declaration.AttributeLists));
        }
        return claims.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> ReadClaims(SyntaxList<AttributeListSyntax> attributeLists)
    {
        var attribute = attributeLists.SelectMany(list => list.Attributes)
            .FirstOrDefault(candidate => IsNamedAttribute(candidate, BravoAuthorizeName));
        if (attribute is null)
            return [];

        return attribute.ArgumentList?.Arguments
            .Select((argument, index) => new { Argument = argument, Index = index })
            .Where(item => item.Argument.NameColon?.Name.Identifier.ValueText == "claims" ||
                           item.Argument.NameColon is null && item.Index == 0)
            .SelectMany(item => item.Argument.Expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
            .Select(expression => expression.Name.Identifier.ValueText)
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
    }

    private static bool IsNamedAttribute(AttributeSyntax attribute, string shortName)
    {
        var name = attribute.Name.ToString().Split('.').Last();
        return name.Equals(shortName, StringComparison.Ordinal) ||
               name.Equals(shortName + "Attribute", StringComparison.Ordinal);
    }

    private static string NormalizeType(string value) =>
        value.Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

    private static string GetShortControllerName(string qualifiedName) => qualifiedName.Split('.').Last();

    private static void WriteWorkbook(
        string outputPath,
        string sheetName,
        IReadOnlyList<ControllerActionExportRow> rows,
        bool overwrite)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add(sheetName);
        string[] headers = ["Controller Name", "Action Name", "Parameter Types", "Method", "Claims"];
        for (var column = 1; column <= headers.Length; column++)
            worksheet.Cells[1, column].Value = headers[column - 1];

        for (var index = 0; index < rows.Count; index++)
        {
            var excelRow = index + 2;
            var row = rows[index];
            worksheet.Cells[excelRow, 1].Value = row.ControllerName;
            worksheet.Cells[excelRow, 2].Value = row.ActionName;
            worksheet.Cells[excelRow, 3].Value = string.Join("; ", row.ParameterTypes);
            worksheet.Cells[excelRow, 4].Value = row.Method;
            worksheet.Cells[excelRow, 5].Value = string.Join("; ", row.Claims);
        }

        var header = worksheet.Cells[1, 1, 1, headers.Length];
        header.Style.Font.Bold = true;
        header.Style.Font.Color.SetColor(Color.White);
        header.Style.Fill.PatternType = ExcelFillStyle.Solid;
        header.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 78, 121));
        header.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        var generated = worksheet.Cells[2, 1, Math.Max(rows.Count + 1, 2), 4];
        generated.Style.Fill.PatternType = ExcelFillStyle.Solid;
        generated.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(242, 242, 242));
        var claims = worksheet.Cells[2, 5, Math.Max(rows.Count + 1, 2), 5];
        claims.Style.Fill.PatternType = ExcelFillStyle.Solid;
        claims.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 242, 204));
        worksheet.Cells[1, 1, Math.Max(rows.Count + 1, 1), headers.Length].AutoFilter = true;
        worksheet.View.FreezePanes(2, 1);
        worksheet.Column(1).Width = 38;
        worksheet.Column(2).Width = 30;
        worksheet.Column(3).Width = 42;
        worksheet.Column(4).Width = 12;
        worksheet.Column(5).Width = 55;
        worksheet.Cells.Style.VerticalAlignment = ExcelVerticalAlignment.Top;
        worksheet.Cells.Style.WrapText = true;
        using var stream = new FileStream(
            outputPath,
            overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        package.SaveAs(stream);
    }
}
