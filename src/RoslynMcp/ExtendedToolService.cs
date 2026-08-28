using System.Reflection;
using RoslynMcp.Core.Refactoring;
using RoslynMcp.Core.Workspace;
using RoslynMcp.Contracts.Models;

namespace RoslynMcp;

// Adapter for the MIT-licensed RoslynMcp.Core operation library.
public static class ExtendedToolService
{
    private static readonly IReadOnlyDictionary<string, (string Operation, string Parameters)> Operations =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["extract-method"] = ("ExtractMethodOperation", "ExtractMethodParams"),
            ["extract-variable"] = ("ExtractVariableOperation", "ExtractVariableParams"),
            ["extract-constant"] = ("ExtractConstantOperation", "ExtractConstantParams"),
            ["extract-interface"] = ("ExtractInterfaceOperation", "ExtractInterfaceParams"),
            ["extract-base-class"] = ("ExtractBaseClassOperation", "ExtractBaseClassParams"),
            ["introduce-parameter"] = ("IntroduceParameterOperation", "IntroduceParameterParams"),
            ["rename-symbol"] = ("RenameSymbolOperation", "RenameSymbolParams"),
            ["inline-variable"] = ("InlineVariableOperation", "InlineVariableParams"),
            ["change-signature"] = ("ChangeSignatureOperation", "ChangeSignatureParams"),
            ["encapsulate-field"] = ("EncapsulateFieldOperation", "EncapsulateFieldParams"),
            ["convert-to-async"] = ("ConvertToAsyncOperation", "ConvertToAsyncParams"),
            ["convert-expression-body"] = ("ConvertExpressionBodyOperation", "ConvertExpressionBodyParams"),
            ["convert-property"] = ("ConvertPropertyOperation", "ConvertPropertyParams"),
            ["convert-foreach-linq"] = ("ConvertForeachLinqOperation", "ConvertForeachLinqParams"),
            ["convert-to-interpolated-string"] = ("ConvertToInterpolatedStringOperation", "ConvertToInterpolatedStringParams"),
            ["convert-to-pattern-matching"] = ("ConvertToPatternMatchingOperation", "ConvertToPatternMatchingParams"),
            ["generate-constructor"] = ("GenerateConstructorOperation", "GenerateConstructorParams"),
            ["generate-equals-hashcode"] = ("GenerateEqualsHashCodeOperation", "GenerateEqualsHashCodeParams"),
            ["generate-overrides"] = ("GenerateOverridesOperation", "GenerateOverridesParams"),
            ["generate-tostring"] = ("GenerateToStringOperation", "GenerateToStringParams"),
            ["implement-interface"] = ("ImplementInterfaceOperation", "ImplementInterfaceParams"),
            ["add-null-checks"] = ("AddNullChecksOperation", "AddNullChecksParams"),
            ["add-missing-usings"] = ("AddMissingUsingsOperation", "AddMissingUsingsParams"),
            ["remove-unused-usings"] = ("RemoveUnusedUsingsOperation", "RemoveUnusedUsingsParams"),
            ["sort-usings"] = ("SortUsingsOperation", "SortUsingsParams"),
            ["format-document"] = ("FormatDocumentOperation", "FormatDocumentParams"),
            ["move-type-to-file"] = ("MoveTypeToFileOperation", "MoveTypeToFileParams"),
            ["move-type-to-namespace"] = ("MoveTypeToNamespaceOperation", "MoveTypeToNamespaceParams"),
            ["find-references"] = ("FindReferencesOperation", "FindReferencesParams"),
            ["find-callers"] = ("FindCallersOperation", "FindCallersParams"),
            ["find-implementations"] = ("FindImplementationsOperation", "FindImplementationsParams"),
            ["go-to-definition"] = ("GoToDefinitionOperation", "GoToDefinitionParams"),
            ["search-symbols"] = ("SearchSymbolsOperation", "SearchSymbolsParams"),
            ["get-diagnostics"] = ("GetDiagnosticsOperation", "GetDiagnosticsParams"),
            ["get-code-metrics"] = ("GetCodeMetricsOperation", "GetCodeMetricsParams"),
            ["analyze-control-flow"] = ("AnalyzeControlFlowOperation", "AnalyzeControlFlowParams"),
            ["analyze-data-flow"] = ("AnalyzeDataFlowOperation", "AnalyzeDataFlowParams"),
            ["get-document-outline"] = ("GetDocumentOutlineOperation", "GetDocumentOutlineParams"),
            ["get-symbol-info"] = ("GetSymbolInfoOperation", "GetSymbolInfoParams"),
            ["get-type-hierarchy"] = ("GetTypeHierarchyOperation", "GetTypeHierarchyParams")
        };

    public static IReadOnlyList<string> GetOperationNames() => ["diagnose", .. Operations.Keys.OrderBy(name => name)];

    public static async Task<object> ExecuteAsync(string path, string operation, object? parameters, CancellationToken cancellationToken = default)
    {
        if (operation.Equals("diagnose", StringComparison.OrdinalIgnoreCase))
            return await DiagnoseAsync(path, cancellationToken);
        if (!Operations.TryGetValue(operation, out var registration))
            throw new ArgumentException($"Unknown operation '{operation}'.", nameof(operation));

        var provider = new MSBuildWorkspaceProvider();
        using var context = await provider.CreateContextAsync(path, cancellationToken);
        var coreAssembly = typeof(MSBuildWorkspaceProvider).Assembly;
        var contractsAssembly = typeof(SearchSymbolsParams).Assembly;
        var operationType = coreAssembly.GetTypes().Single(type => type.Name == registration.Operation);
        var parameterType = contractsAssembly.GetTypes().Single(type => type.Name == registration.Parameters);
        if (parameters is null || !parameterType.IsInstanceOfType(parameters))
            throw new ArgumentException($"Operation '{operation}' requires parameters of type {parameterType.Name}.", nameof(parameters));
        var instance = Activator.CreateInstance(operationType, context)!;
        var method = operationType.GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.Public)!;
        var task = (Task)method.Invoke(instance, [parameters, cancellationToken])!;
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        return result;
    }

    private static async Task<object> DiagnoseAsync(string path, CancellationToken cancellationToken)
    {
        var provider = new MSBuildWorkspaceProvider();
        var environment = provider.CheckEnvironment();
        if (string.IsNullOrWhiteSpace(path))
            return new { healthy = environment.MsBuildFound, environment, workspace = (object?)null, error = (object?)null };

        try
        {
            using var context = await provider.CreateContextAsync(path, cancellationToken);
            return new
            {
                healthy = environment.MsBuildFound,
                environment,
                workspace = (object)new { loaded = true, path = context.LoadedPath, projects = context.Solution.ProjectIds.Count },
                error = (object?)null
            };
        }
        catch (RefactoringException ex)
        {
            return new
            {
                healthy = false,
                environment,
                workspace = (object)new { loaded = false, path },
                error = (object)new { code = ex.ErrorCode, message = ex.Message, details = ex.Details, suggestions = ex.Suggestions }
            };
        }
    }
}
