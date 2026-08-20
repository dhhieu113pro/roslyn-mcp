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

| Tool | What it does | Result |
|------|--------------|--------|
| `add-authorize-attribute` | Reads controller/action claim mappings from Excel and previews or adds the requested authorization attribute. | Preview/applied state, row-by-row statuses, validation conflicts, summary counts, generated attributes, and changed files. |
| `export-controller-actions` | Scans controller actions and exports an editable Excel workbook for HTTP-method review and claim entry. | Workbook path and sheet, exported/skipped counts, and every exported controller/action row. |

### Navigation and analysis

| Tool | What it does | Result |
|------|--------------|--------|
| `diagnose` | Checks MSBuild/Roslyn availability and optionally verifies that a workspace loads. | Health flag, environment details, and loaded workspace path/project count. |
| `find-references` | Finds reads, writes, and other references to a symbol across the solution. | Resolved symbol identity, reference locations and context, total count, and truncation state. |
| `find-callers` | Finds methods and members that call a selected method. | Resolved method identity, caller locations, total count, and truncation state. |
| `find-implementations` | Finds interface implementations and overrides of abstract or virtual members. | Resolved symbol identity, implementation locations, total count, and truncation state. |
| `go-to-definition` | Resolves a symbol use to one or more source declarations. | Definition locations, including multiple locations for partial declarations. |
| `search-symbols` | Searches workspace declarations by name with an optional symbol-kind filter. | Query, matching symbols and locations, total count, and truncation state. |
| `get-diagnostics` | Retrieves compiler diagnostics for the solution or a selected source file. | Diagnostic IDs, messages, severity and locations, plus the total count. |
| `get-code-metrics` | Calculates maintainability and complexity metrics for a symbol. | Cyclomatic complexity, lines of code, maintainability index, coupling, and inheritance depth. |
| `analyze-control-flow` | Analyzes reachability and exits within a selected source region. | Start/end reachability, return statements, and exit points. |
| `analyze-data-flow` | Analyzes how variables move through a selected source region. | Variables read, written, flowing in/out, captured, and always assigned. |
| `get-document-outline` | Builds a hierarchical outline of declarations in a C# file. | File path, namespace/type/member outline entries, and total count. |
| `get-symbol-info` | Returns semantic metadata for a selected symbol. | Symbol kind, qualified name, accessibility, modifiers, types, parameters, members, and documentation when available. |
| `get-type-hierarchy` | Traverses base types, derived types, and implemented interfaces. | Type identity and its base types, derived types, and interfaces. |

### Extract, move, and signature refactorings

| Tool | What it does | Result |
|------|--------------|--------|
| `extract-method` | Extracts a selected statement or expression region into a new method. | Preview/apply status, generated method and call-site file changes, symbol metadata, and errors. |
| `extract-variable` | Replaces a selected expression with a new local variable. | Preview/apply status, declaration/replacement changes, symbol metadata, and errors. |
| `extract-constant` | Replaces a selected literal with a named constant, optionally replacing all matches. | Preview/apply status, constant/replacement changes, reference count, and errors. |
| `extract-interface` | Creates an interface from selected members of a type and optionally implements it. | Preview/apply status, new/updated files, generated interface metadata, and errors. |
| `extract-base-class` | Moves selected members into a new base class. | Preview/apply status, new/updated files, generated base-type metadata, and errors. |
| `introduce-parameter` | Promotes a local variable to a method parameter and updates callers. | Preview/apply status, declaration/call-site changes, updated reference count, and errors. |
| `rename-symbol` | Renames a symbol and updates its references, implementations, overloads, or file as requested. | Preview/apply status, changed files, renamed symbol metadata, updated reference count, and errors. |
| `inline-variable` | Replaces uses of a local variable with its initializer and removes the declaration. | Preview/apply status, replacements, updated reference count, and errors. |
| `change-signature` | Adds, removes, renames, retypes, or reorders method parameters and updates callers. | Preview/apply status, method/call-site changes, updated reference count, and errors. |
| `encapsulate-field` | Wraps a field in a property and updates field references. | Preview/apply status, property/reference changes, symbol metadata, and errors. |
| `move-type-to-file` | Moves a type declaration into a target source file. | Preview/apply status, source/target file changes, moved symbol metadata, and errors. |
| `move-type-to-namespace` | Changes a type's namespace and updates references and using directives. | Preview/apply status, file/reference changes, using counts, and errors. |

### Conversions

| Tool | What it does | Result |
|------|--------------|--------|
| `convert-to-async` | Converts a synchronous method to async/await form and optionally adds the `Async` suffix. | Preview/apply status, method and caller changes, updated reference count, and errors. |
| `convert-expression-body` | Converts a member between expression-bodied and block-bodied syntax. | Preview/apply status, member file changes, symbol metadata, and errors. |
| `convert-property` | Converts a property between auto-property and full-property forms. | Preview/apply status, property/backing-field changes, symbol metadata, and errors. |
| `convert-foreach-linq` | Converts a compatible `foreach` accumulation pattern into LINQ. | Preview/apply status, loop replacement changes, symbol metadata, and errors. |
| `convert-to-interpolated-string` | Converts compatible concatenation or `string.Format` code to interpolation. | Preview/apply status, expression changes, symbol metadata, and errors. |
| `convert-to-pattern-matching` | Converts compatible type checks or switches to C# pattern matching. | Preview/apply status, control-structure changes, symbol metadata, and errors. |

### Generation, organization, and formatting

| Tool | What it does | Result |
|------|--------------|--------|
| `generate-constructor` | Generates a constructor for selected fields or properties, with optional null guards. | Preview/apply status, constructor file changes, generated symbol metadata, and errors. |
| `generate-equals-hashcode` | Generates equality members from selected fields or properties. | Preview/apply status, generated `Equals`/`GetHashCode` changes, symbol metadata, and errors. |
| `generate-overrides` | Generates overrides for selected virtual or abstract base members. | Preview/apply status, generated member changes, symbol metadata, and errors. |
| `generate-tostring` | Generates a `ToString` override from selected fields or properties. | Preview/apply status, generated method changes, symbol metadata, and errors. |
| `implement-interface` | Generates implicit or explicit implementations for interface members. | Preview/apply status, generated member changes, symbol metadata, and errors. |
| `add-null-checks` | Adds null guards for eligible method parameters. | Preview/apply status, guard changes, symbol metadata, and errors. |
| `add-missing-usings` | Adds using directives needed to resolve unbound types in one file or the solution. | Preview/apply status, changed files, number of usings added, and errors. |
| `remove-unused-usings` | Removes unnecessary using directives from one file or the solution. | Preview/apply status, changed files, number of usings removed, and errors. |
| `sort-usings` | Orders using directives in a C# source file. | Preview/apply status, reordered file changes, and errors. |
| `format-document` | Formats a C# source file with the workspace's Roslyn formatting options. | Success status, formatted file changes, execution time, and errors. |

Mutating tools support the `preview` contract provided by `RoslynMcp.Core`.
Request a preview first and apply only after reviewing the returned changes.
`format-document` follows the underlying operation's immediate-apply behavior.

## Tool input examples

These examples use working-directory-relative paths. Replace the sample solution,
source files, symbols, and line positions with values from your workspace.

### Authorization automation examples

| Tool | Example input |
|------|---------------|
| `add-authorize-attribute` | `{"path":"MyProduct.sln","parameters":{"authorizeAttributeName":"BravoAuthorize","excelPath":"authorization.xlsx","sheetName":"Permissions","preview":true}}` |
| `export-controller-actions` | `{"path":"MyProduct.sln","parameters":{"excelPath":"controller-actions.xlsx","sheetName":"Actions","includeAuthorized":false,"overwrite":false}}` |

### Navigation and analysis examples

| Tool | Example input |
|------|---------------|
| `diagnose` | `{"path":"MyProduct.sln"}` |
| `find-references` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","symbolName":"OrderService","maxResults":50}}` |
| `find-callers` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","symbolName":"CreateOrderAsync","line":42,"maxResults":50}}` |
| `find-implementations` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/IOrderService.cs","symbolName":"IOrderService","maxResults":50}}` |
| `go-to-definition` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderController.cs","symbolName":"IOrderService","line":18,"column":22}}` |
| `search-symbols` | `{"path":"MyProduct.sln","parameters":{"query":"PaymentService","kindFilter":"Class","maxResults":20}}` |
| `get-diagnostics` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","severityFilter":"Warning"}}` |
| `get-code-metrics` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","symbolName":"CreateOrderAsync","line":42}}` |
| `analyze-control-flow` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","startLine":42,"endLine":60}}` |
| `analyze-data-flow` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","startLine":42,"endLine":60}}` |
| `get-document-outline` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs"}}` |
| `get-symbol-info` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","symbolName":"OrderService","line":8}}` |
| `get-type-hierarchy` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","symbolName":"OrderService","direction":"Both"}}` |

### Extract, move, and signature refactoring examples

| Tool | Example input |
|------|---------------|
| `extract-method` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","startLine":42,"startColumn":9,"endLine":55,"endColumn":10,"methodName":"ValidateOrder","visibility":"private","preview":true}}` |
| `extract-variable` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","startLine":48,"startColumn":22,"endLine":48,"endColumn":46,"variableName":"total","useVar":true,"preview":true}}` |
| `extract-constant` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","startLine":15,"startColumn":30,"endLine":15,"endColumn":35,"constantName":"MaxRetries","visibility":"private","replaceAll":true,"preview":true}}` |
| `extract-interface` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","typeName":"OrderService","interfaceName":"IOrderService","members":["CreateOrderAsync"],"targetFile":"src/IOrderService.cs","addInterfaceToType":true,"preview":true}}` |
| `extract-base-class` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","typeName":"OrderService","baseClassName":"OrderServiceBase","members":["ValidateOrder"],"targetFile":"src/OrderServiceBase.cs","makeAbstract":true,"preview":true}}` |
| `introduce-parameter` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","variableName":"timeout","line":42,"preview":true}}` |
| `rename-symbol` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","symbolName":"OrderService","newName":"OrderProcessor","renameFile":true,"preview":true}}` |
| `inline-variable` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","variableName":"total","line":48,"preview":true}}` |
| `change-signature` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","methodName":"CreateOrderAsync","line":42,"parameters":[{"originalName":"order","name":"request","type":"CreateOrderRequest","newPosition":0}],"preview":true}}` |
| `encapsulate-field` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/Order.cs","fieldName":"_status","propertyName":"Status","readOnly":false,"preview":true}}` |
| `move-type-to-file` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/Models.cs","symbolName":"Order","targetFile":"src/Order.cs","createTargetFile":true,"preview":true}}` |
| `move-type-to-namespace` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/Order.cs","symbolName":"Order","targetNamespace":"MyProduct.Domain","updateFileLocation":false,"preview":true}}` |

### Conversion examples

| Tool | Example input |
|------|---------------|
| `convert-to-async` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","methodName":"CreateOrder","line":42,"renameToAsync":true,"preview":true}}` |
| `convert-expression-body` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/Order.cs","memberName":"GetTotal","direction":"ToBlock","preview":true}}` |
| `convert-property` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/Order.cs","propertyName":"Status","direction":"ToFull","preview":true}}` |
| `convert-foreach-linq` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","line":64,"preview":true}}` |
| `convert-to-interpolated-string` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","line":72,"preview":true}}` |
| `convert-to-pattern-matching` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","line":80,"preview":true}}` |

### Generation, organization, and formatting examples

| Tool | Example input |
|------|---------------|
| `generate-constructor` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","typeName":"OrderService","members":["_repository","_logger"],"addNullChecks":true,"preview":true}}` |
| `generate-equals-hashcode` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/Order.cs","typeName":"Order","fields":["Id","Number"],"preview":true}}` |
| `generate-overrides` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderHandler.cs","typeName":"OrderHandler","members":["HandleAsync"],"callBase":false,"preview":true}}` |
| `generate-tostring` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/Order.cs","typeName":"Order","fields":["Id","Number"],"format":"interpolated","preview":true}}` |
| `implement-interface` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","typeName":"OrderService","interfaceName":"IOrderService","explicitImplementation":false,"members":["CreateOrderAsync"],"throwNotImplemented":true,"preview":true}}` |
| `add-null-checks` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","methodName":"CreateOrderAsync","line":42,"style":"ThrowIfNull","preview":true}}` |
| `add-missing-usings` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","allFiles":false,"preview":true}}` |
| `remove-unused-usings` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","allFiles":false,"preview":true}}` |
| `sort-usings` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs","preview":true}}` |
| `format-document` | `{"path":"MyProduct.sln","parameters":{"sourceFile":"src/OrderService.cs"}}` |

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
