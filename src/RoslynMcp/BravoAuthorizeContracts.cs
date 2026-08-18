using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace RoslynMcp;

[ExcludeFromCodeCoverage(Justification = "Serialization contracts contain no executable behavior.")]
public sealed class AddBravoAuthorizeParams
{
    [Description("Absolute or working-directory-relative path to the .xlsx authorization mapping.")]
    public required string ExcelPath { get; init; }

    [Description("Worksheet name. When omitted, the first worksheet is used.")]
    public string? SheetName { get; init; }

    [Description("Preview changes without writing files. Defaults to true.")]
    public bool Preview { get; init; } = true;
}

[ExcludeFromCodeCoverage(Justification = "Serialization contracts contain no executable behavior.")]
public sealed record BravoAuthorizeMapping(
    int ExcelRow,
    string ControllerName,
    string ActionName,
    IReadOnlyList<string> Claims,
    IReadOnlyList<string>? ParameterTypes = null)
{
    public BravoAuthorizeMapping(
        int excelRow,
        string controllerName,
        string actionName,
        IReadOnlyList<string> claims)
        : this(excelRow, controllerName, actionName, claims, null) { }

    public void Deconstruct(
        out int excelRow,
        out string controllerName,
        out string actionName,
        out IReadOnlyList<string> claims)
    {
        excelRow = ExcelRow;
        controllerName = ControllerName;
        actionName = ActionName;
        claims = Claims;
    }
}

[ExcludeFromCodeCoverage(Justification = "Serialization contracts contain no executable behavior.")]
public sealed record BravoAuthorizeRowResult(
    int ExcelRow,
    string Controller,
    string Action,
    IReadOnlyList<string> Claims,
    string Status,
    string? File = null,
    int? Line = null,
    string? Message = null,
    string? GeneratedAttribute = null);

[ExcludeFromCodeCoverage(Justification = "Serialization contracts contain no executable behavior.")]
public sealed record BravoAuthorizeSummary(
    int ExcelRows,
    int Matched,
    int Changed,
    int Unchanged,
    int Conflicts,
    int Errors);

[ExcludeFromCodeCoverage(Justification = "Serialization contracts contain no executable behavior.")]
public sealed record AddBravoAuthorizeResult(
    bool Preview,
    bool Applied,
    BravoAuthorizeSummary Summary,
    IReadOnlyList<BravoAuthorizeRowResult> Rows,
    IReadOnlyList<string> ChangedFiles);

[ExcludeFromCodeCoverage(Justification = "Serialization contracts contain no executable behavior.")]
public sealed class ExportControllerActionsParams
{
    [Description("Absolute or working-directory-relative destination path for the .xlsx review workbook.")]
    public required string ExcelPath { get; init; }

    [Description("Worksheet name. Defaults to Actions.")]
    public string SheetName { get; init; } = "Actions";

    [Description("Include actions that already have BravoAuthorize and prefill their claims. Defaults to false.")]
    public bool IncludeAuthorized { get; init; }

    [Description("Replace an existing workbook at ExcelPath. Defaults to false.")]
    public bool Overwrite { get; init; }
}

[ExcludeFromCodeCoverage(Justification = "Serialization contracts contain no executable behavior.")]
public sealed record ControllerActionExportRow(
    string ControllerName,
    string ActionName,
    IReadOnlyList<string> ParameterTypes,
    string Method,
    IReadOnlyList<string> Claims,
    string File,
    int Line);

[ExcludeFromCodeCoverage(Justification = "Serialization contracts contain no executable behavior.")]
public sealed record ExportControllerActionsResult(
    string ExcelPath,
    string SheetName,
    int ExportedActions,
    int SkippedAuthorized,
    int SkippedAnonymous,
    IReadOnlyList<ControllerActionExportRow> Rows);
