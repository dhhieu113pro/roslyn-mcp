using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using RoslynMcp.Core.Workspace;
using System.Diagnostics.CodeAnalysis;

namespace RoslynMcp;

[ExcludeFromCodeCoverage(Justification = "Roslyn workspace integration is covered by end-to-end fixture tests.")]
public static class BravoAuthorizeService
{
    private const string AttributeName = "BravoAuthorize";
    private const string ClaimTypeName = "BravoClaimConstants";
    private const string GeneratedAnnotationKind = "BravoAuthorizeGenerated";

    public static async Task<AddBravoAuthorizeResult> ExecuteAsync(
        string path,
        AddBravoAuthorizeParams parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var mappings = BravoAuthorizeExcelReader.Read(parameters.ExcelPath, parameters.SheetName);
        var provider = new MSBuildWorkspaceProvider();
        using var context = await provider.CreateContextAsync(path, cancellationToken);
        var solution = context.Solution;
        var rows = new List<BravoAuthorizeRowResult>();
        var touchedDocuments = new HashSet<DocumentId>();

        var duplicateRows = FindDuplicateRows(mappings);
        foreach (var mapping in mappings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (duplicateRows.TryGetValue(mapping.ExcelRow, out var duplicateMessage))
            {
                rows.Add(CreateResult(mapping, "duplicate-row", message: duplicateMessage));
                continue;
            }

            var controllerMatches = await FindControllersAsync(solution, mapping.ControllerName, cancellationToken);
            if (controllerMatches.Count == 0)
            {
                rows.Add(CreateResult(mapping, "controller-not-found", message: $"Controller '{mapping.ControllerName}' was not found."));
                continue;
            }
            if (controllerMatches.Count > 1)
            {
                rows.Add(CreateResult(mapping, "ambiguous-controller", message: $"Controller '{mapping.ControllerName}' matched {controllerMatches.Count} declarations."));
                continue;
            }

            var controller = controllerMatches[0];
            var actionMatches = controller.Symbol.GetMembers(mapping.ActionName)
                .OfType<IMethodSymbol>()
                .Where(IsControllerAction)
                .Where(method => mapping.ParameterTypes is null ||
                    ControllerActionExporter.ParameterTypesMatch(method, mapping.ParameterTypes))
                .ToArray();
            if (actionMatches.Length == 0)
            {
                var signature = mapping.ParameterTypes is null
                    ? mapping.ActionName
                    : $"{mapping.ActionName}({string.Join(", ", mapping.ParameterTypes)})";
                rows.Add(CreateResult(mapping, "action-not-found", message: $"Action '{signature}' was not found in '{controller.Symbol.Name}'."));
                continue;
            }
            if (actionMatches.Length > 1)
            {
                rows.Add(CreateResult(mapping, "ambiguous-action", message: $"Action '{mapping.ActionName}' has {actionMatches.Length} overloads in '{controller.Symbol.Name}'."));
                continue;
            }

            var action = actionMatches[0];
            var document = solution.GetDocument(action.DeclaringSyntaxReferences[0].SyntaxTree)!;
            var method = (MethodDeclarationSyntax)await action.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
            var invalidClaims = await FindInvalidClaimsAsync(document, method.SpanStart, mapping.Claims, cancellationToken);
            if (invalidClaims.Count > 0)
            {
                rows.Add(CreateResult(mapping, "invalid-claim", document.FilePath,
                    GetLine(action), $"Claims were not found as const members of {ClaimTypeName}: {string.Join(", ", invalidClaims)}."));
                continue;
            }

            var existing = method.AttributeLists.SelectMany(list => list.Attributes)
                .Where(IsBravoAuthorizeAttribute)
                .ToArray();
            if (existing.Length > 1)
            {
                rows.Add(CreateResult(mapping, "conflict", document.FilePath, GetLine(action), "Action has multiple BravoAuthorize attributes."));
                continue;
            }

            var controllerAttributes = await GetControllerBravoAuthorizeAttributesAsync(controller.Symbol, cancellationToken);
            if (controllerAttributes.Length > 1)
            {
                rows.Add(CreateResult(mapping, "conflict", document.FilePath, GetLine(action),
                    "Controller has multiple BravoAuthorize attributes."));
                continue;
            }
            if (existing.Length == 1 || controllerAttributes.Length == 1)
            {
                var effectiveClaims = new HashSet<string>(StringComparer.Ordinal);
                if (controllerAttributes.Length == 1)
                    effectiveClaims.UnionWith(ReadExistingClaims(controllerAttributes[0]));
                if (existing.Length == 1)
                    effectiveClaims.UnionWith(ReadExistingClaims(existing[0]));

                if (effectiveClaims.SetEquals(mapping.Claims))
                    rows.Add(CreateResult(mapping, "unchanged", document.FilePath, GetLine(action),
                        "Action is already protected by the effective BravoAuthorize attributes."));
                else
                    rows.Add(CreateResult(mapping, "conflict", document.FilePath, GetLine(action),
                        "Existing action or controller BravoAuthorize attributes have different effective claims."));
                continue;
            }

            var attributeText = BuildAttributeText(mapping.Claims);
            var attributeList = SyntaxFactory.ParseCompilationUnit($"{attributeText}\nclass Placeholder {{ }}")
                .DescendantNodes().OfType<AttributeListSyntax>().Single()
                .WithAdditionalAnnotations(Formatter.Annotation, new SyntaxAnnotation(GeneratedAnnotationKind));
            var changedMethod = method.AddAttributeLists(attributeList);
            var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            var changedDocument = document.WithSyntaxRoot(root.ReplaceNode(method, changedMethod));
            changedDocument = await Formatter.FormatAsync(changedDocument, Formatter.Annotation, cancellationToken: cancellationToken);
            changedDocument = await PolishGeneratedAttributeAsync(changedDocument, mapping.Claims, cancellationToken);
            solution = changedDocument.Project.Solution;
            touchedDocuments.Add(document.Id);
            rows.Add(CreateResult(mapping, "preview", document.FilePath, GetLine(action), generatedAttribute: attributeText));
        }

        var hasBlockingIssues = rows.Any(row => row.Status is not ("preview" or "unchanged"));
        IReadOnlyList<string> changedFiles = [];
        var applied = false;
        if (!parameters.Preview && !hasBlockingIssues && touchedDocuments.Count > 0)
        {
            var commit = await context.CommitChangesAsync(solution, cancellationToken);
            if (!commit.Success)
                throw new InvalidOperationException(commit.Error ?? "Failed to commit BravoAuthorize changes.");
            changedFiles = commit.FilesModified;
            applied = true;
            rows = rows.Select(row => row.Status == "preview" ? row with { Status = "applied" } : row).ToList();
        }

        return new(parameters.Preview, applied, CreateSummary(rows), rows, changedFiles);
    }

    private static async Task<IReadOnlyList<ControllerMatch>> FindControllersAsync(
        Solution solution,
        string requestedName,
        CancellationToken cancellationToken)
    {
        var requested = requestedName.Trim();
        var requestedShortName = requested.Split('.').Last();
        var candidateNames = requestedShortName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
            ? new[] { requestedShortName }
            : new[] { requestedShortName, requestedShortName + "Controller" };
        var matches = new List<ControllerMatch>();

        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
            foreach (var candidateName in candidateNames)
            {
                var declarations = await SymbolFinder.FindDeclarationsAsync(
                    project, candidateName, ignoreCase: true, SymbolFilter.Type, cancellationToken);
                matches.AddRange(declarations.OfType<INamedTypeSymbol>()
                    .Where(symbol => symbol.TypeKind == TypeKind.Class && IsRequestedController(symbol, requested, candidateNames))
                    .Select(symbol => new ControllerMatch(symbol)));
            }

        return matches.DistinctBy(match => match.Symbol, SymbolEqualityComparer.Default).ToArray();
    }

    private static bool IsRequestedController(INamedTypeSymbol symbol, string requested, IReadOnlyList<string> candidateNames)
    {
        var isController = symbol.Name.EndsWith("Controller", StringComparison.Ordinal) ||
                           HasAttribute(symbol, "Controller");
        if (!isController || HasAttribute(symbol, "NonController") ||
            !candidateNames.Contains(symbol.Name, StringComparer.OrdinalIgnoreCase))
            return false;
        if (!requested.Contains('.'))
            return true;

        var qualified = symbol.ToDisplayString();
        return qualified.Equals(requested, StringComparison.OrdinalIgnoreCase) ||
               qualified.Equals(requested + "Controller", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAttribute(ISymbol symbol, string shortName) =>
        symbol.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.Name is { } name &&
            (name.Equals(shortName, StringComparison.Ordinal) ||
             name.Equals(shortName + "Attribute", StringComparison.Ordinal)));

    private static bool IsControllerAction(IMethodSymbol method) =>
        method.MethodKind == MethodKind.Ordinary &&
        method.DeclaredAccessibility == Accessibility.Public &&
        !method.IsStatic &&
        !method.IsImplicitlyDeclared &&
        method.DeclaringSyntaxReferences.Length == 1 &&
        !method.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.Name is "NonAction" or "NonActionAttribute");

    private static async Task<IReadOnlyList<string>> FindInvalidClaimsAsync(
        Document document,
        int position,
        IReadOnlyList<string> claims,
        CancellationToken cancellationToken)
    {
        var semanticModel = (await document.GetSemanticModelAsync(cancellationToken))!;
        return claims.Where(claim =>
        {
            var expression = SyntaxFactory.ParseExpression(claim);
            var symbol = semanticModel.GetSpeculativeSymbolInfo(
                position, expression, SpeculativeBindingOption.BindAsExpression).Symbol;
            return symbol is not IFieldSymbol { IsConst: true };
        }).ToArray();
    }

    private static bool IsBravoAuthorizeAttribute(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString().Split('.').Last();
        return name is AttributeName or AttributeName + "Attribute";
    }

    private static HashSet<string> ReadExistingClaims(AttributeSyntax attribute) =>
        attribute.ArgumentList?.Arguments
            .Select((argument, index) => new { Argument = argument, Index = index })
            .Where(item => item.Argument.NameColon?.Name.Identifier.ValueText == "claims" ||
                           item.Argument.NameColon is null && item.Index == 0)
            .SelectMany(item => item.Argument.Expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
            .Select(expression => expression.ToString().Replace(" ", string.Empty, StringComparison.Ordinal))
            .Where(value => value.StartsWith(ClaimTypeName + ".", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal) ?? new(StringComparer.Ordinal);

    private static async Task<AttributeSyntax[]> GetControllerBravoAuthorizeAttributesAsync(
        INamedTypeSymbol controller,
        CancellationToken cancellationToken)
    {
        var attributes = new List<AttributeSyntax>();
        foreach (var reference in controller.DeclaringSyntaxReferences)
        {
            if (await reference.GetSyntaxAsync(cancellationToken) is TypeDeclarationSyntax declaration)
                attributes.AddRange(declaration.AttributeLists.SelectMany(list => list.Attributes)
                    .Where(IsBravoAuthorizeAttribute));
        }
        return attributes.ToArray();
    }

    private static string BuildAttributeText(IReadOnlyList<string> claims)
    {
        var lines = claims.Select(claim => $"        {claim}");
        return $"[{AttributeName}(\n    claims:\n    [\n{string.Join(",\n", lines)}\n    ])]";
    }

    private static async Task<Document> PolishGeneratedAttributeAsync(
        Document document,
        IReadOnlyList<string> claims,
        CancellationToken cancellationToken)
    {
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var formatted = root.GetAnnotatedNodes(GeneratedAnnotationKind).OfType<AttributeListSyntax>().Single();
        var leadingText = formatted.GetLeadingTrivia().ToFullString();
        var indentation = leadingText[(leadingText.LastIndexOfAny(['\r', '\n']) + 1)..];
        var options = await document.GetOptionsAsync(cancellationToken);
        var indentationUnit = options.GetOption(FormattingOptions.UseTabs, LanguageNames.CSharp)
            ? "\t"
            : new string(' ', options.GetOption(FormattingOptions.IndentationSize, LanguageNames.CSharp));
        var continuation = indentation + indentationUnit;
        var itemIndentation = continuation + indentationUnit;
        var lines = claims.Select(claim => $"{itemIndentation}{claim}");
        var text = $"[{AttributeName}(\n{continuation}claims:\n{continuation}[\n{string.Join(",\n", lines)}\n{continuation}])]";
        var polished = SyntaxFactory.ParseCompilationUnit($"{text}\nclass Placeholder {{ }}")
            .DescendantNodes().OfType<AttributeListSyntax>().Single()
            .WithLeadingTrivia(formatted.GetLeadingTrivia())
            .WithTrailingTrivia(formatted.GetTrailingTrivia());
        return document.WithSyntaxRoot(root.ReplaceNode(formatted, polished));
    }

    private static Dictionary<int, string> FindDuplicateRows(IReadOnlyList<BravoAuthorizeMapping> mappings)
    {
        var duplicates = new Dictionary<int, string>();
        foreach (var group in mappings.GroupBy(mapping =>
                     $"{NormalizeController(mapping.ControllerName)}|{mapping.ActionName.Trim()}|{FormatMappingParameterTypes(mapping.ParameterTypes)}",
                     StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
        {
            var rows = string.Join(", ", group.Select(mapping => mapping.ExcelRow));
            var message = $"Duplicate controller/action mapping appears on Excel rows {rows}.";
            foreach (var mapping in group)
                duplicates[mapping.ExcelRow] = message;
        }
        return duplicates;
    }

    private static string NormalizeController(string name) =>
        name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ? name : name + "Controller";

    private static string FormatMappingParameterTypes(IReadOnlyList<string>? parameterTypes) =>
        parameterTypes is null ? "<unspecified>" : string.Join(";", parameterTypes.Select(type => type.Replace(" ", string.Empty, StringComparison.Ordinal)));

    private static BravoAuthorizeRowResult CreateResult(
        BravoAuthorizeMapping mapping,
        string status,
        string? file = null,
        int? line = null,
        string? message = null,
        string? generatedAttribute = null) =>
        new(mapping.ExcelRow, mapping.ControllerName, mapping.ActionName, mapping.Claims, status, file, line, message, generatedAttribute);

    private static int GetLine(IMethodSymbol method) =>
        method.Locations[0].GetLineSpan().StartLinePosition.Line + 1;

    private static BravoAuthorizeSummary CreateSummary(IReadOnlyList<BravoAuthorizeRowResult> rows) => new(
        rows.Count,
        rows.Count(row => row.File is not null),
        rows.Count(row => row.Status is "preview" or "applied"),
        rows.Count(row => row.Status == "unchanged"),
        rows.Count(row => row.Status is "conflict" or "duplicate-row"),
        rows.Count(row => row.Status is "controller-not-found" or "action-not-found" or
            "ambiguous-controller" or "ambiguous-action" or "invalid-claim"));

    private sealed record ControllerMatch(INamedTypeSymbol Symbol);
}
