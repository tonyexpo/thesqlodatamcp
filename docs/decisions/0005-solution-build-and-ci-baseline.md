# ADR 0005 — Solution, build, and CI baseline

- **Status:** Accepted
- **Date:** 2026-07-20

## Context

Milestone 0 requires a production solution whose boundaries match the target architecture, whose validated library choices are pinned centrally, and whose build and test policy can run consistently locally and in CI. The research projects under `spikes/` are compatibility evidence and must not become production dependencies or enter the production solution.

## Decision

- Use `thesqlodatamcp.slnx` with exactly the five source projects and four test projects named by ADR 0002.
- Preserve this production dependency direction:
  - `TheSqlODataMcp.Core` has no product-project dependency;
  - `TheSqlODataMcp.SqlServer`, `TheSqlODataMcp.Persistence`, and `TheSqlODataMcp.Protocols` depend on Core;
  - `TheSqlODataMcp.Web` is the composition root and depends on all four boundaries.
- Keep all spike projects outside `thesqlodatamcp.slnx`.
- Target .NET 10 and C# 14. Pin SDK feature band `10.0.110` in `global.json` with `latestPatch` roll-forward.
- Use NuGet Central Package Management. Pin the ADR 0003 library baselines, the already compiled `Microsoft.Data.SqlClient` 6.1.1 baseline, and the validated test packages in `Directory.Packages.props`; project files contain no package versions.
- Put catalog input libraries in Core, `Microsoft.Data.SqlClient` in SqlServer, MCP/OData libraries in Protocols, and the ASP.NET Core OpenIddict server integration in Web. Persistence remains free of an unselected EF Core or control-store provider until Milestone 1 makes and verifies that choice.
- Apply nullable analysis, implicit usings, warnings-as-errors, SDK analyzers, build-time code-style enforcement, and deterministic compilation through shared repository settings.
- Track a handoff-shaped `appsettings.json` with blank connection strings, public URL, and bootstrap token. Ignore local configuration and secret overrides.
- Run restore, warning-free build, all four test projects, formatting verification, and an offline repository-local Markdown link check in CI.

## Consequences

- New production projects or reverse dependencies require an explicit architectural justification.
- Protocol and provider packages cannot create a path around Core and the future CQM boundary.
- Package upgrades require central changes and the corresponding spike or regression evidence.
- The workflow is locally validated, but Milestone 0 still requires its first successful run on the intended GitHub Actions runner.
- SQL Server Testcontainers acceptance remains governed by proposed ADR 0004 and is not implied by this solution baseline.
