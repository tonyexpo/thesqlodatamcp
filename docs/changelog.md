# Changelog

## [Unreleased]

### Added
- Initial project setup.
- MCP connector concept for SQL DQL queries.
- Public project handoff documenting the transition from Qwen 3.6 35B to Codex 5.6 Terra.
- A verified project-status document and an initial recovery-first backlog.
- Imported the complete original `AI_DATA_GATEWAY_HANDOFF.md` as the authoritative product and architecture baseline, with an identical SHA-256 hash to the supplied source file.
- Added a milestone-based v1 roadmap with explicit outcomes and exit gates.
- Added the annotated historical tag `legacy-poc-final-2026-07-18` at the final committed PoC state.
- Added ADR 0001, recording `thesqlodatamcp` as the definitive product/repository name and closing repository/licensing decisions.
- Added ADR 0002, selecting `TheSqlODataMcp` for .NET projects, assemblies, and namespaces and `thesqlodatamcp.slnx` for the solution file.
- Added executable .NET 10 spikes for MCP Streamable HTTP and structured content, runtime OData EDM without EF, OpenIddict APIs, strict Markdown/YAML/JSON Schema validation, and disposable SQL Server testing.
- Added ADR 0003 for the accepted MCP, OData, OpenIddict, and catalog-library baseline.
- Added proposed ADR 0004 for Testcontainers-based SQL Server integration testing, pending a successful real Docker and CI run.
- Added a version-controlled technical-lead skill, root `AGENTS.md`, and development-state checkpoint so architecture, QA evidence, open gates, and next work survive conversational resets.
- Added the production `thesqlodatamcp.slnx` baseline with five source projects and four test projects, preserving the approved dependency directions and keeping research spikes outside the solution.
- Added central package management, shared .NET 10/C# 14 build policy, nullable analysis, warnings-as-errors, analyzers, formatting rules, deterministic compilation, and SDK pinning.
- Added safe handoff-shaped example configuration, local-secret ignore conventions, six deterministic scaffold tests, an offline Markdown link verifier, and a CI workflow for restore, build, tests, formatting, and documentation links.
- Added ADR 0005 recording the accepted solution, dependency, package-placement, build, configuration, and CI baseline.
- Added a versioned provider-neutral reporting-catalog contract with 8,128 deterministic rows across twelve tables, portable relationship/feature expectations, and explicit SQL Server extensions.
- Added SQL Server reset/bootstrap and teardown scripts covering multiple schemas, composite and ambiguous relationships, computed and temporal columns, broad type metadata, keyless views, descriptions, and programmable objects that future introspection must exclude.
- Extended the SQL Server spike with external-server and owned-Testcontainers modes, static contract/`GO` parser tests, metadata/data assertions, guaranteed fixed-database cleanup, and a dedicated Docker-capable CI job.
- Isolated spike package pins from production Central Package Management so every research spike remains independently restorable.

### Changed
- Corrected the interpretation of v0.6.0: the project compiles, but MCP tool discovery and end-to-end interoperability were not verified. The current `McpTools` class is not marked with the SDK-required `McpToolType` attribute.
- Reclassified the current implementation as an incomplete proof of concept, not a deployable read-only SQL connector.
- Reworked README, architecture, backlog, and project status around the authoritative AI Data Gateway baseline.
- Replaced the earlier incremental MCP → OData → “ATP JSON” assumption with the settled architecture: MCP Streamable HTTP, OData 4.01, JSON/HTTP `QUERY`, and one shared Canonical Query Model.
- Confirmed Apache License 2.0 and retention of this existing public repository for the clean implementation.
- Replaced the “AI Data Gateway” working title in current project documentation with the definitive public name `thesqlodatamcp`; the original imported handoff remains unchanged.

### Removed
- Removed the obsolete legacy C# project, static settings, unit-test project, and accidentally tracked `bin/`/`obj/` artifacts from `main`. They remain recoverable from `legacy-poc-final-2026-07-18` and Git history.
- Removed the obsolete PoC agent-handoff and QA documents from `main` to keep the active documentation focused on the clean implementation. Their history remains available in Git.

### Known limitations
- Current stdout diagnostics are incompatible with MCP stdio and expose secrets.
- Current bearer-token configuration is not real client authentication or OAuth.
- The current DQL blacklist and free-form `WHERE` input do not constitute a safe read-only boundary.
- OData and the JSON query API are not implemented by the legacy proof of concept; both are now defined as v1 adapters over the shared CQM.

## [v0.6.0 - MCP Server Hosting Attempt]

### MCP Server & Tools Integration
- Added a compiling attempt to wire up the ModelContextProtocol server transport and tool registration in `Program.cs`.
- Used Microsoft.Extensions.Hosting's generic host pattern (`Host.CreateApplicationBuilder`) to initialize the application.
- Applied `[McpToolAttribute]` to three methods and used `AddMcpServer`, `WithStdioServerTransport`, and `WithToolsFromAssembly`.
- The project compiles successfully with zero errors, but the class-level `[McpToolType]` marker and a protocol-level verification were omitted; this version must not be described as completed MCP tool registration.

## [v0.5.1 - Phase 5 Structure & Authentication (Transport/Tools Placeholder)]

### MCP Server & Authentication
- Completed Phase 5 structure: Bearer token authentication validation implemented using the token from `settings.json`.
- MCP server initialization structure prepared using `ModelContextProtocol.Server`.
- Note: Transport initialization (stdio) and tool registration execution are left as placeholders pending full SDK API alignment or a compatible version of the `ModelContextProtocol` package, as the current version (0.1.0-preview.1.25171.12) does not expose `ModelContextProtocol.Protocol.Models` or `ModelContextProtocol.Transport.Stdio`.

## [v0.5.0 - Phase 3, 4 & 5 Completion: SqlClient Connector, MCP Tools Execution & Server Initialization Structure]

### Database Connector & MCP Tools
- Completed Phase 3: Implemented `DatabaseConnector` with `ListTables()` and `GetTableSchema(tableName)` using `SqlConnection` and `SqlCommand`.
- Completed Phase 4: Implemented `McpTools.ExecuteDqlQueryAsync` to execute validated DQL queries via `SqlClient` with parameterized conditions.
- JSON condition processing is now supported in `execute_dql_query`, converting JSON filters to parameterized SQL WHERE clauses.
- Security enforcement ensures all values are passed as pure parameters via `SqlCommand.Parameters`.

## [v0.2.0 - Architectural Decisions & v1 Scope]

### Platform & Stack
- Target framework updated to .NET 10 (`net10.0`).
- Database access restricted to direct ADO.NET `SqlClient` for MS SQL Server (no EF/ORM).
- MCP integration via the `modelcontextprotocol` NuGet package.

### Security & Auth
- Implemented strict DQL-only enforcement to prevent SQL injection and data modification (Option A: T-SQL DQL Parser & Validator).
- Authentication simplified to simple Bearer token authentication with a direct token saved locally in a settings file for v1.

### MCP Tools (v1)
- Exposed tools: `list_tables`, `get_table_schema(table_name)`, `execute_dql_query(table_name, where_conditions_json_or_sql)`.
