# RoslynMcp

RoslynMcp is a .NET 10 stdio server exposing semantic C# navigation, analysis,
generation, and refactoring through the Model Context Protocol. It uses the
official C# MCP SDK 2.0 and implements the 2026-07-28 protocol while retaining
the SDK's compatibility with older MCP clients.

## Run

```bash
dotnet run --project src/RoslynMcp/RoslynMcp.csproj
```

The process reserves stdout for MCP JSON-RPC messages. Configure your MCP client
to launch the command above with this repository as its working directory.

Every tool accepts `path`, the absolute or working-directory-relative path to a
`.sln`, `.slnx`, or `.csproj`. Operation-specific fields are supplied in the
typed `parameters` object according to the generated MCP input schema.

## Tools

RoslynMcp exposes exactly these 41 tools.

### Navigation and analysis

1. `diagnose`
2. `find-references`
3. `find-callers`
4. `find-implementations`
5. `go-to-definition`
6. `search-symbols`
7. `get-diagnostics`
8. `get-code-metrics`
9. `analyze-control-flow`
10. `analyze-data-flow`
11. `get-document-outline`
12. `get-symbol-info`
13. `get-type-hierarchy`

### Extract, move, and signature refactorings

14. `extract-method`
15. `extract-variable`
16. `extract-constant`
17. `extract-interface`
18. `extract-base-class`
19. `introduce-parameter`
20. `rename-symbol`
21. `inline-variable`
22. `change-signature`
23. `encapsulate-field`
24. `move-type-to-file`
25. `move-type-to-namespace`

### Conversions

26. `convert-to-async`
27. `convert-expression-body`
28. `convert-property`
29. `convert-foreach-linq`
30. `convert-to-interpolated-string`
31. `convert-to-pattern-matching`

### Generation, organization, and formatting

32. `generate-constructor`
33. `generate-equals-hashcode`
34. `generate-overrides`
35. `generate-tostring`
36. `implement-interface`
37. `add-null-checks`
38. `add-missing-usings`
39. `remove-unused-usings`
40. `sort-usings`
41. `format-document`

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

## Build and test

```bash
dotnet build RoslynMcp.slnx
dotnet test RoslynMcp.slnx --configuration Release
```

The tests enforce the exact 41-tool discovery surface and exercise the Roslyn
services. The integration suite launches the built server over stdio, negotiates
MCP, lists all tools, and invokes `diagnose`.

The repository-scoped `roslyn-investigate` skill instructs Codex to prefer these
semantic tools for C# symbol and relationship investigation.
