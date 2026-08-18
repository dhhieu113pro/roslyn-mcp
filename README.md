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

RoslynMcp exposes exactly these 42 tools.

### Authorization automation

1. `add-bravo-authorize`

### Navigation and analysis

2. `diagnose`
3. `find-references`
4. `find-callers`
5. `find-implementations`
6. `go-to-definition`
7. `search-symbols`
8. `get-diagnostics`
9. `get-code-metrics`
10. `analyze-control-flow`
11. `analyze-data-flow`
12. `get-document-outline`
13. `get-symbol-info`
14. `get-type-hierarchy`

### Extract, move, and signature refactorings

15. `extract-method`
16. `extract-variable`
17. `extract-constant`
18. `extract-interface`
19. `extract-base-class`
20. `introduce-parameter`
21. `rename-symbol`
22. `inline-variable`
23. `change-signature`
24. `encapsulate-field`
25. `move-type-to-file`
26. `move-type-to-namespace`

### Conversions

27. `convert-to-async`
28. `convert-expression-body`
29. `convert-property`
30. `convert-foreach-linq`
31. `convert-to-interpolated-string`
32. `convert-to-pattern-matching`

### Generation, organization, and formatting

33. `generate-constructor`
34. `generate-equals-hashcode`
35. `generate-overrides`
36. `generate-tostring`
37. `implement-interface`
38. `add-null-checks`
39. `add-missing-usings`
40. `remove-unused-usings`
41. `sort-usings`
42. `format-document`

Mutating tools support the `preview` contract provided by `RoslynMcp.Core`.
Request a preview first and apply only after reviewing the returned changes.
`format-document` follows the underlying operation's immediate-apply behavior.

## Example tool input

`search-symbols`:

```json
{
  "path": "samples/SkillFixture/SkillFixture.slnx",
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

`add-bravo-authorize` preview:

```json
{
  "path": "MyProduct.sln",
  "parameters": {
    "excelPath": "authorization.xlsx",
    "sheetName": "Permissions",
    "preview": true
  }
}
```

The `.xlsx` worksheet must contain `Controller Name`, `Action Name`, and `Claims`
columns. Separate multiple claims with commas, semicolons, or line breaks. Short
claim names such as `Companies_Retrieve` are normalized to direct constant
references such as `BravoClaimConstants.Companies_Retrieve`. The generated
attribute contains only the `claims` argument. Preview is the default, existing
conflicting attributes block the whole batch, and apply writes nothing unless
every row validates successfully.

## Build and test

```bash
dotnet build RoslynMcp.slnx
dotnet test RoslynMcp.slnx --configuration Release
```

The tests enforce the exact 42-tool discovery surface and exercise the Roslyn
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
