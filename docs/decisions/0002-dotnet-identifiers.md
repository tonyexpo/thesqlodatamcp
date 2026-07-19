# ADR 0002 — .NET solution and identifier naming

- **Status:** Accepted
- **Date:** 2026-07-19

## Context

[ADR 0001](./0001-project-identity.md) established `thesqlodatamcp` as the definitive public product and repository name, while leaving the casing of .NET identifiers open until scaffolding.

The solution needs idiomatic, readable identifiers that map unambiguously to the public name without changing that name on user-facing surfaces.

## Decision

- Use **`TheSqlODataMcp`** as the root .NET identifier and namespace.
- Name the solution file `thesqlodatamcp.slnx` so the repository entry point matches the public product name exactly.
- Name source projects and assemblies:
  - `TheSqlODataMcp.Core`;
  - `TheSqlODataMcp.SqlServer`;
  - `TheSqlODataMcp.Persistence`;
  - `TheSqlODataMcp.Protocols`;
  - `TheSqlODataMcp.Web`.
- Name test projects by appending `.Tests` to the corresponding boundary, with `TheSqlODataMcp.IntegrationTests` and `TheSqlODataMcp.ProtocolTests` for cross-project suites.
- Keep public documentation, release names, package/container naming, configuration prefixes, and user-facing surfaces based on the definitive lowercase name `thesqlodatamcp` unless a target ecosystem imposes another convention.

## Consequences

- Namespaces and assemblies remain readable while preserving the `Sql`, `OData`, and `Mcp` components of the product identity.
- The solution filename is easy to discover from the repository root.
- Project names replace the provisional `Gateway.*` labels in the concise architecture map.
- Future projects must follow the same root identifier and still require a justified dependency boundary.
