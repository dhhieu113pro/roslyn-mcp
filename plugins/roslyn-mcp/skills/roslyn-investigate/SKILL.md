---
name: roslyn-investigate
description: Investigate C# declarations, references, callers, implementations, diagnostics, and code structure through RoslynMcp tools. Prefer this over text search for C# symbols and relationships.
---

# Roslyn Investigate

Use RoslynMcp results as the primary evidence when locating or tracing C# symbols.

## Investigate

1. Select the nearest `.sln`, `.slnx`, or `.csproj` containing the code and pass it as `path`.
2. Start with `search-symbols` using the smallest meaningful query and an appropriate `kindFilter`.
3. For relationships, call `find-references`, `find-callers`, `find-implementations`, `go-to-definition`, or `get-type-hierarchy`.
4. Use `get-symbol-info`, `get-document-outline`, `get-code-metrics`, `analyze-control-flow`, `analyze-data-flow`, or `get-diagnostics` for deeper inspection.
5. Read only the returned source locations needed to answer the request.
6. Explain findings with qualified symbol names and clickable source locations.

## Interpret results

- An empty result means no matching source declaration was found; it does not prove the concept is absent.
- Distinguish overloads and partial declarations by qualified name and source location.
- Do not infer callers, references, or implementations from declaration results; run the corresponding semantic operation.
- Report workspace-load and compilation failures explicitly.
- Use text search only for literals, configuration, generated artifacts, or semantic-load failures.
- Do not modify source during investigation unless the user separately requests a change.
- Keep `preview` enabled for mutating tools until applying the change is clearly authorized.
