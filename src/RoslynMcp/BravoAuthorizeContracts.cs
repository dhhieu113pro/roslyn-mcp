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
public sealed record BravoAuthorizeMapping(int ExcelRow, string ControllerName, string ActionName, IReadOnlyList<string> Claims);

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
