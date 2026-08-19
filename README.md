# RoslynMcp

<!-- mcp-name: io.github.dhhieu113pro/roslyn-mcp -->

[![NuGet](https://img.shields.io/nuget/v/RoslynMcp.Dnx.svg)](https://www.nuget.org/packages/RoslynMcp.Dnx/)
[![CI](https://github.com/dhhieu113pro/roslyn-mcp/actions/workflows/ci.yml/badge.svg)](https://github.com/dhhieu113pro/roslyn-mcp/actions/workflows/ci.yml)

RoslynMcp is a .NET 10 stdio server exposing semantic C# navigation, analysis,
generation, and refactoring through the Model Context Protocol. It uses the
official C# MCP SDK 2.0 and implements the 2026-07-28 protocol while retaining
the SDK's compatibility with older MCP clients.

## Run from NuGet with `dnx`

Install the .NET 10 SDK, then run the latest release directly from NuGet.org:

```bash
dnx RoslynMcp.Dnx --yes
```

For reproducible MCP configuration, pin a version:

```json
{
  "servers": {
    "roslyn": {
      "type": "stdio",
      "command": "dnx",
      "args": ["RoslynMcp.Dnx@1.0.0", "--yes"]
    }
  }
}
```

The NuGet package ID includes `.Dnx` because `RoslynMcp` is owned by a different
publisher on NuGet.org. The executable tool command remains `RoslynMcp`.

## Run from source

```bash
dotnet run --project src/RoslynMcp/RoslynMcp.csproj
```

The process reserves stdout for MCP JSON-RPC messages. Configure your MCP client
to launch the command above with this repository as its working directory.

Every tool accepts `path`, the absolute or working-directory-relative path to a
`.sln`, `.slnx`, or `.csproj`. Operation-specific fields are supplied in the
typed `parameters` object according to the generated MCP input schema.

## Tools

RoslynMcp exposes exactly these 43 tools.

### Authorization automation

1. `add-authorize-attribute`
2. `export-controller-actions`

### Navigation and analysis

3. `diagnose`
4. `find-references`
5. `find-callers`
6. `find-implementations`
7. `go-to-definition`
8. `search-symbols`
9. `get-diagnostics`
10. `get-code-metrics`
11. `analyze-control-flow`
12. `analyze-data-flow`
13. `get-document-outline`
14. `get-symbol-info`
15. `get-type-hierarchy`

### Extract, move, and signature refactorings

16. `extract-method`
17. `extract-variable`
18. `extract-constant`
19. `extract-interface`
20. `extract-base-class`
21. `introduce-parameter`
22. `rename-symbol`
23. `inline-variable`
24. `change-signature`
25. `encapsulate-field`
26. `move-type-to-file`
27. `move-type-to-namespace`

### Conversions

28. `convert-to-async`
29. `convert-expression-body`
30. `convert-property`
31. `convert-foreach-linq`
32. `convert-to-interpolated-string`
33. `convert-to-pattern-matching`

### Generation, organization, and formatting

34. `generate-constructor`
35. `generate-equals-hashcode`
36. `generate-overrides`
37. `generate-tostring`
38. `implement-interface`
39. `add-null-checks`
40. `add-missing-usings`
41. `remove-unused-usings`
42. `sort-usings`
43. `format-document`

Mutating tools support the `preview` contract provided by `RoslynMcp.Core`.
Request a preview first and apply only after reviewing the returned changes.
`format-document` follows the underlying operation's immediate-apply behavior.

## Example tool input

`search-symbols`:

```json
{
  "path": "MyProduct.sln",
  "parameters": {
    "query": "PaymentService",
    "kindFilter": "Class",
    "maxResults": 20
  }
}
```

`rename-symbol` preview:

```json
{
  "path": "MyProduct.sln",
  "parameters": {
    "sourceFile": "/repo/src/OrderService.cs",
    "symbolName": "OrderService",
    "newName": "OrderProcessor",
    "preview": true
  }
}
```

`add-authorize-attribute` preview:

```json
{
  "path": "MyProduct.sln",
  "parameters": {
    "authorizeAttributeName": "BravoAuthorize",
    "excelPath": "authorization.xlsx",
    "sheetName": "Permissions",
    "preview": true
  }
}
```

`export-controller-actions` review workbook:

```json
{
  "path": "MyProduct.sln",
  "parameters": {
    "excelPath": "controller-actions.xlsx",
    "sheetName": "Actions",
    "includeAuthorized": false,
    "overwrite": false
  }
}
```

The exported workbook contains `Controller Name`, `Action Name`, `Parameter
Types`, `Method`, and `Claims`. It exports declared public controller actions,
excludes `[NonAction]` and `[AllowAnonymous]`, records GET/POST for review, and
highlights the Claims column for input. By default, actions that already have
`[BravoAuthorize]` are omitted. Set `includeAuthorized` to `true` to include them
with their current claims prefilled. The export never changes source code.

`authorizeAttributeName` is required and identifies the attribute to recognize and
generate (for example, `BravoAuthorize` or `BravoAuthorizeAttribute`). The `.xlsx`
worksheet must contain `Controller Name`, `Action Name`, and `Claims`
columns. Separate multiple claims with commas, semicolons, or line breaks. Short
claim names such as `Companies_Retrieve` are normalized to direct constant
references such as `BravoClaimConstants.Companies_Retrieve`. The generated
attribute contains only the `claims` argument. Preview is the default, existing
conflicting attributes block the whole batch, and apply writes nothing unless
every row validates successfully. Scanner workbooks may also contain `Parameter
Types` for overload matching and a review-only `Method` column.

## Build and test

```bash
dotnet build RoslynMcp.slnx
dotnet test RoslynMcp.slnx --configuration Release
```

The tests enforce the exact 43-tool discovery surface and exercise the Roslyn
services. The integration suite launches the built server over stdio, negotiates
MCP, lists all tools, and invokes `diagnose`.

The repository-scoped `roslyn-investigate` skill instructs Codex to prefer these
semantic tools for C# symbol and relationship investigation.

## Release

The `ci.yml` workflow builds and exercises a NuGet package on every pull request
and push. A SemVer tag on `main` additionally publishes the already-verified
package to NuGet.org through trusted publishing:

```bash
git tag -a v1.0.0 -m "v1.0.0"
git push origin v1.0.0
```

The workflow derives the NuGet version, embedded MCP manifest version, assembly
version, and MCP handshake version from the tag. The protected GitHub
environment must remain named `production`, and the trusted-publishing workflow
must remain `.github/workflows/ci.yml`.

## License

RoslynMcp is licensed under the [MIT License](LICENSE). Third-party license
details are recorded in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
