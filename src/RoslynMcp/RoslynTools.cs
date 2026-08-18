using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using ModelContextProtocol.Server;
using RoslynMcp.Contracts.Models;

namespace RoslynMcp;

[McpServerToolType]
[ExcludeFromCodeCoverage(Justification = "Declarative MCP adapters are covered by the tool-surface and protocol integration tests.")]
public sealed class RoslynTools
{
    [McpServerTool(Name = "add-bravo-authorize"), Description("Add BravoAuthorize claim attributes to controller actions from an Excel mapping, with preview by default.")]
    public static Task<AddBravoAuthorizeResult> AddBravoAuthorize(
        string path,
        AddBravoAuthorizeParams parameters,
        CancellationToken cancellationToken) =>
        BravoAuthorizeService.ExecuteAsync(path, parameters, cancellationToken);

    [McpServerTool(Name = "diagnose"), Description("Check the Roslyn/MSBuild environment and optionally load a workspace.")]
    public static Task<object> Diagnose(string path, CancellationToken cancellationToken) =>
        ExtendedToolService.ExecuteAsync(path, "diagnose", null, cancellationToken);

    [McpServerTool(Name = "extract-method"), Description("Extract selected C# statements into a method.")]
    public static Task<object> ExtractMethod(string path, ExtractMethodParams parameters, CancellationToken ct) => Run(path, "extract-method", parameters, ct);
    [McpServerTool(Name = "extract-variable"), Description("Extract a selected C# expression into a variable.")]
    public static Task<object> ExtractVariable(string path, ExtractVariableParams parameters, CancellationToken ct) => Run(path, "extract-variable", parameters, ct);
    [McpServerTool(Name = "extract-constant"), Description("Extract a selected C# expression into a constant.")]
    public static Task<object> ExtractConstant(string path, ExtractConstantParams parameters, CancellationToken ct) => Run(path, "extract-constant", parameters, ct);
    [McpServerTool(Name = "extract-interface"), Description("Extract selected members from a type into an interface.")]
    public static Task<object> ExtractInterface(string path, ExtractInterfaceParams parameters, CancellationToken ct) => Run(path, "extract-interface", parameters, ct);
    [McpServerTool(Name = "extract-base-class"), Description("Extract selected members from a type into a base class.")]
    public static Task<object> ExtractBaseClass(string path, ExtractBaseClassParams parameters, CancellationToken ct) => Run(path, "extract-base-class", parameters, ct);
    [McpServerTool(Name = "introduce-parameter"), Description("Introduce a method parameter from a local value.")]
    public static Task<object> IntroduceParameter(string path, IntroduceParameterParams parameters, CancellationToken ct) => Run(path, "introduce-parameter", parameters, ct);
    [McpServerTool(Name = "rename-symbol"), Description("Rename a C# symbol throughout its solution.")]
    public static Task<object> RenameSymbol(string path, RenameSymbolParams parameters, CancellationToken ct) => Run(path, "rename-symbol", parameters, ct);
    [McpServerTool(Name = "inline-variable"), Description("Inline a local variable.")]
    public static Task<object> InlineVariable(string path, InlineVariableParams parameters, CancellationToken ct) => Run(path, "inline-variable", parameters, ct);
    [McpServerTool(Name = "change-signature"), Description("Change a method signature and update callers.")]
    public static Task<object> ChangeSignature(string path, ChangeSignatureParams parameters, CancellationToken ct) => Run(path, "change-signature", parameters, ct);
    [McpServerTool(Name = "encapsulate-field"), Description("Encapsulate a field as a property.")]
    public static Task<object> EncapsulateField(string path, EncapsulateFieldParams parameters, CancellationToken ct) => Run(path, "encapsulate-field", parameters, ct);
    [McpServerTool(Name = "convert-to-async"), Description("Convert a method to asynchronous form.")]
    public static Task<object> ConvertToAsync(string path, ConvertToAsyncParams parameters, CancellationToken ct) => Run(path, "convert-to-async", parameters, ct);
    [McpServerTool(Name = "convert-expression-body"), Description("Convert between expression-bodied and block-bodied members.")]
    public static Task<object> ConvertExpressionBody(string path, ConvertExpressionBodyParams parameters, CancellationToken ct) => Run(path, "convert-expression-body", parameters, ct);
    [McpServerTool(Name = "convert-property"), Description("Convert between auto and full properties.")]
    public static Task<object> ConvertProperty(string path, ConvertPropertyParams parameters, CancellationToken ct) => Run(path, "convert-property", parameters, ct);
    [McpServerTool(Name = "convert-foreach-linq"), Description("Convert a foreach construct to LINQ.")]
    public static Task<object> ConvertForeachLinq(string path, ConvertForeachLinqParams parameters, CancellationToken ct) => Run(path, "convert-foreach-linq", parameters, ct);
    [McpServerTool(Name = "convert-to-interpolated-string"), Description("Convert string construction to an interpolated string.")]
    public static Task<object> ConvertToInterpolatedString(string path, ConvertToInterpolatedStringParams parameters, CancellationToken ct) => Run(path, "convert-to-interpolated-string", parameters, ct);
    [McpServerTool(Name = "convert-to-pattern-matching"), Description("Convert compatible C# code to pattern matching.")]
    public static Task<object> ConvertToPatternMatching(string path, ConvertToPatternMatchingParams parameters, CancellationToken ct) => Run(path, "convert-to-pattern-matching", parameters, ct);
    [McpServerTool(Name = "generate-constructor"), Description("Generate a constructor for selected members.")]
    public static Task<object> GenerateConstructor(string path, GenerateConstructorParams parameters, CancellationToken ct) => Run(path, "generate-constructor", parameters, ct);
    [McpServerTool(Name = "generate-equals-hashcode"), Description("Generate equality and hash-code members.")]
    public static Task<object> GenerateEqualsHashCode(string path, GenerateEqualsHashCodeParams parameters, CancellationToken ct) => Run(path, "generate-equals-hashcode", parameters, ct);
    [McpServerTool(Name = "generate-overrides"), Description("Generate overrides for inherited members.")]
    public static Task<object> GenerateOverrides(string path, GenerateOverridesParams parameters, CancellationToken ct) => Run(path, "generate-overrides", parameters, ct);
    [McpServerTool(Name = "generate-tostring"), Description("Generate a ToString implementation.")]
    public static Task<object> GenerateToString(string path, GenerateToStringParams parameters, CancellationToken ct) => Run(path, "generate-tostring", parameters, ct);
    [McpServerTool(Name = "implement-interface"), Description("Implement an interface on a C# type.")]
    public static Task<object> ImplementInterface(string path, ImplementInterfaceParams parameters, CancellationToken ct) => Run(path, "implement-interface", parameters, ct);
    [McpServerTool(Name = "add-null-checks"), Description("Add null guards to a method.")]
    public static Task<object> AddNullChecks(string path, AddNullChecksParams parameters, CancellationToken ct) => Run(path, "add-null-checks", parameters, ct);
    [McpServerTool(Name = "add-missing-usings"), Description("Add missing using directives.")]
    public static Task<object> AddMissingUsings(string path, AddMissingUsingsParams parameters, CancellationToken ct) => Run(path, "add-missing-usings", parameters, ct);
    [McpServerTool(Name = "remove-unused-usings"), Description("Remove unused using directives.")]
    public static Task<object> RemoveUnusedUsings(string path, RemoveUnusedUsingsParams parameters, CancellationToken ct) => Run(path, "remove-unused-usings", parameters, ct);
    [McpServerTool(Name = "sort-usings"), Description("Sort using directives.")]
    public static Task<object> SortUsings(string path, SortUsingsParams parameters, CancellationToken ct) => Run(path, "sort-usings", parameters, ct);
    [McpServerTool(Name = "format-document"), Description("Format a C# document.")]
    public static Task<object> FormatDocument(string path, FormatDocumentParams parameters, CancellationToken ct) => Run(path, "format-document", parameters, ct);
    [McpServerTool(Name = "move-type-to-file"), Description("Move a type declaration to another file.")]
    public static Task<object> MoveTypeToFile(string path, MoveTypeToFileParams parameters, CancellationToken ct) => Run(path, "move-type-to-file", parameters, ct);
    [McpServerTool(Name = "move-type-to-namespace"), Description("Move a type to another namespace.")]
    public static Task<object> MoveTypeToNamespace(string path, MoveTypeToNamespaceParams parameters, CancellationToken ct) => Run(path, "move-type-to-namespace", parameters, ct);
    [McpServerTool(Name = "find-references"), Description("Find references to a C# symbol.")]
    public static Task<object> FindReferences(string path, FindReferencesParams parameters, CancellationToken ct) => Run(path, "find-references", parameters, ct);
    [McpServerTool(Name = "find-callers"), Description("Find callers of a C# method.")]
    public static Task<object> FindCallers(string path, FindCallersParams parameters, CancellationToken ct) => Run(path, "find-callers", parameters, ct);
    [McpServerTool(Name = "find-implementations"), Description("Find implementations of a C# symbol.")]
    public static Task<object> FindImplementations(string path, FindImplementationsParams parameters, CancellationToken ct) => Run(path, "find-implementations", parameters, ct);
    [McpServerTool(Name = "go-to-definition"), Description("Locate the definition at a source position.")]
    public static Task<object> GoToDefinition(string path, GoToDefinitionParams parameters, CancellationToken ct) => Run(path, "go-to-definition", parameters, ct);
    [McpServerTool(Name = "search-symbols"), Description("Search C# symbols semantically.")]
    public static Task<object> SearchSymbols(string path, SearchSymbolsParams parameters, CancellationToken ct) => Run(path, "search-symbols", parameters, ct);
    [McpServerTool(Name = "get-diagnostics"), Description("Get compiler and analyzer diagnostics.")]
    public static Task<object> GetDiagnostics(string path, GetDiagnosticsParams parameters, CancellationToken ct) => Run(path, "get-diagnostics", parameters, ct);
    [McpServerTool(Name = "get-code-metrics"), Description("Calculate code metrics for a symbol.")]
    public static Task<object> GetCodeMetrics(string path, GetCodeMetricsParams parameters, CancellationToken ct) => Run(path, "get-code-metrics", parameters, ct);
    [McpServerTool(Name = "analyze-control-flow"), Description("Analyze control flow in a source region.")]
    public static Task<object> AnalyzeControlFlow(string path, AnalyzeControlFlowParams parameters, CancellationToken ct) => Run(path, "analyze-control-flow", parameters, ct);
    [McpServerTool(Name = "analyze-data-flow"), Description("Analyze data flow in a source region.")]
    public static Task<object> AnalyzeDataFlow(string path, AnalyzeDataFlowParams parameters, CancellationToken ct) => Run(path, "analyze-data-flow", parameters, ct);
    [McpServerTool(Name = "get-document-outline"), Description("Get the semantic outline of a C# document.")]
    public static Task<object> GetDocumentOutline(string path, GetDocumentOutlineParams parameters, CancellationToken ct) => Run(path, "get-document-outline", parameters, ct);
    [McpServerTool(Name = "get-symbol-info"), Description("Get detailed semantic information about a symbol.")]
    public static Task<object> GetSymbolInfo(string path, GetSymbolInfoParams parameters, CancellationToken ct) => Run(path, "get-symbol-info", parameters, ct);
    [McpServerTool(Name = "get-type-hierarchy"), Description("Get the inheritance hierarchy of a C# type.")]
    public static Task<object> GetTypeHierarchy(string path, GetTypeHierarchyParams parameters, CancellationToken ct) => Run(path, "get-type-hierarchy", parameters, ct);

    private static Task<object> Run(string path, string operation, object parameters, CancellationToken cancellationToken) =>
        ExtendedToolService.ExecuteAsync(path, operation, parameters, cancellationToken);
}
