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
- Added ADR 0004 for Testcontainers-based SQL Server integration testing; it is now accepted after the real owned-container path passed on GitHub Actions.
- Added a version-controlled technical-lead skill, root `AGENTS.md`, and development-state checkpoint so architecture, QA evidence, open gates, and next work survive conversational resets.
- Added the production `thesqlodatamcp.slnx` baseline with five source projects and four test projects, preserving the approved dependency directions and keeping research spikes outside the solution.
- Added central package management, shared .NET 10/C# 14 build policy, nullable analysis, warnings-as-errors, analyzers, formatting rules, deterministic compilation, and SDK pinning.
- Added safe handoff-shaped example configuration, local-secret ignore conventions, six deterministic scaffold tests, an offline Markdown link verifier, and a CI workflow for restore, build, tests, formatting, and documentation links.
- Added ADR 0005 recording the accepted solution, dependency, package-placement, build, configuration, and CI baseline.
- Added a versioned provider-neutral reporting-catalog contract with 8,128 deterministic rows across twelve tables, portable relationship/feature expectations, and explicit SQL Server extensions.
- Added SQL Server reset/bootstrap and teardown scripts covering multiple schemas, composite and ambiguous relationships, computed and temporal columns, broad type metadata, keyless views, descriptions, and programmable objects that future introspection must exclude.
- Extended the SQL Server spike with external-server and owned-Testcontainers modes, static contract/`GO` parser tests, metadata/data assertions, guaranteed fixed-database cleanup, and a dedicated Docker-capable CI job.
- Isolated spike package pins from production Central Package Management so every research spike remains independently restorable.
- Verified the first GitHub Actions run on the intended Ubuntu runner: production validation and the dependent disposable SQL Server integration job both passed, closing Milestone 0.
- Added the provider-neutral technical Catalog Core with stable physical identities, canonical/provider types, fields, keys, indexes, relationships, keyless views, temporal metadata, construction-time invariants, canonical JSON, and deterministic SHA-256 structural hashes.
- Added ADR 0006 recording the accepted technical-catalog representation and the boundaries left for later Milestone 1 slices.
- Added a deterministic SQL Server catalog type mapper with strict metadata validation, preserved provider details, conservative `unknown` behavior, and unit coverage across supported scalar and storage boundaries.
- Added ADR 0007 recording the accepted SQL Server type-mapping boundary and explicit-unknown policy.
- Added the first SQL Server introspection slice for user tables, views, columns, provider types, descriptions, identity/computed/temporal/rowversion flags, deterministic projection, and structural exclusions.
- Added a production integration test that runs the introspector twice against the deterministic SQL Server fixture and verifies catalog shape, exclusions, metadata, stable canonical JSON/hash, and teardown.
- Added ADR 0008, accepted after the production introspector and deterministic fixture passed on the Docker-capable GitHub Actions runner.
- Added SQL Server primary/alternate key, useful standalone index, and foreign-key introspection through one fixed read-only multi-result command.
- Added ordered projection and validation for composite keys, index key fields, self/multiple/composite relationships, and relationship target integrity.
- Extended the deterministic SQL Server fixture and production integration assertions with a standalone composite index, filtered-index behavior, constraint-backing index exclusion, and representative relational metadata.
- Added ADR 0009 for the relational-metadata discovery boundary, accepted after the production introspector's key, index, and foreign-key projection passed on the Docker-capable GitHub Actions runner.
- Added `MergedCatalog`, `MergedEntity`, `MergedField`, and `MergedRelationship` to `TheSqlODataMcp.Core`, produced by a new `CatalogMerger.Merge(TechnicalCatalog, SemanticOverlay?)`: physical entities annotated with overlay display metadata, an effective key (overlay `odata.key` override, else physical primary key, else keyless), and a discovered/configured relationship union, each entity's physical object always reachable and never lost.
- Added `MergedCatalogCanonicalJson` for deterministic canonical JSON and a structural SHA-256 hash of the merged view, sensitive to every overlay-attributable change.
- Added ADR 0011 for the merge-precedence boundary, accepted after GitHub Actions run 32064882285 passed both `validate` and `sqlserver-integration`.

### Fixed
- Fixed `CatalogMerger` silently accepting a semantic overlay's `odata.key` list containing a duplicate field name (e.g. `key: [Id, Id]`), which produced a malformed `MergedEntity.EffectiveKeyFields` composite key with a repeated field instead of failing; found during an independent post-acceptance QA audit of ADRs 0006–0011. Added `CatalogMergeErrorCodes.ODataKeyFieldDuplicate`, a merge-time duplicate check that fires independently alongside the existing not-found check, and `minLength`/`uniqueItems` on the embedded JSON Schema's `odata.key` definition as defense-in-depth for the YAML-import path. See ADR 0011's subsequent-evidence note.
- Bumped `Testcontainers.MsSql` from 4.8.1 to 4.14.0 (centrally and in the independently pinned SQL Server test spike) after the previous version's transitive `SSH.NET` 2024.2.0 dependency triggered a CI-blocking `NU1903` high-severity advisory during `dotnet restore`. Updated `SqlServerReportingCatalogFixture.cs` and the spike's `MsSqlContainerTests.cs` for `MsSqlBuilder`'s new non-obsolete constructor, which now requires the image explicitly rather than defaulting one. Confirmed by GitHub Actions run 32059087651 (both `validate` and `sqlserver-integration` passed); see ADR 0004's subsequent-evidence note.
- Fixed `CatalogMerger` throwing an unhandled exception instead of returning a `CatalogMergeResult` failure when overlay content contained whitespace-only entity display names/names or a whitespace-only relationship YAML key; the latter now has its own error code, `merge.relationshipNameInvalid`.
- Fixed a thread-safety defect in `SemanticOverlayImporter` (ADR 0010): its shared static `JsonSchema` instance was not safe to evaluate concurrently, measured at roughly 42% silent validation-bypass under concurrent load; fixed by serializing schema evaluation. See ADR 0010's subsequent-evidence note.

### Changed
- Normalized SQL Server's fixed-width `sys.objects.type` catalog value at the query boundary so user tables and views reach strict projection as deterministic `U`/`V` values.
- Verified the table/view/column introspection foundation end to end in GitHub Actions, including deterministic repeat discovery, metadata/exclusion assertions, and fixture teardown.
- Corrected the interpretation of v0.6.0: the project compiles, but MCP tool discovery and end-to-end interoperability were not verified. The current `McpTools` class is not marked with the SDK-required `McpToolType` attribute.
- Reclassified the current implementation as an incomplete proof of concept, not a deployable read-only SQL connector.
- Reworked README, architecture, backlog, and project status around the authoritative AI Data Gateway baseline.
- Replaced the earlier incremental MCP → OData → “ATP JSON” assumption with the settled architecture: MCP Streamable HTTP, OData 4.01, JSON/HTTP `QUERY`, and one shared Canonical Query Model.
- Confirmed Apache License 2.0 and retention of this existing public repository for the clean implementation.
- Replaced the “AI Data Gateway” working title in current project documentation with the definitive public name `thesqlodatamcp`; the original imported handoff remains unchanged.
- Moved the recurring SQL Server CI gate from the real API spike path to the production introspector path while retaining the spike's static fixture checks.
- Replaced the introspector's empty key/index/relationship projection with complete physical relational metadata while preserving the single fixed-command and provider-neutral boundaries.
- Verified the complete SQL Server introspection foundation (tables, views, columns, keys, indexes, foreign keys) end to end in GitHub Actions on the real deterministic fixture, closing the Milestone 1 introspection backlog item.
- Added semantic overlay Markdown/YAML front-matter import and strict validation (`SemanticOverlay`, `SemanticOverlayImporter`) to `TheSqlODataMcp.Core`: dual-stage strict-YAML-deserialization plus versioned-JSON-Schema validation, rejection of the six forbidden top-level sections, physical-reference validation against a discovered `TechnicalCatalog`, and a collected-errors result type instead of throw-per-violation. Import/validation only; overlay merge into the technical catalog is a later slice.
- Added ADR 0010 for the semantic overlay import/validation boundary, accepted after GitHub Actions run 32059087651 passed both `validate` and `sqlserver-integration`.
- Updated `AGENTS.md`, the `thesqlodatamcp-technical-lead` skill, and `docs/development-state.md`'s operating model to a conditional implementation-assignment policy: Claude Code sessions running with Ultracode's Dynamic Workflow now own development, independent QA, architecture, and documentation directly, dynamically choosing per task whether to implement directly (with mandatory independent sub-agent review before acceptance) or assign a dynamically chosen sub-agent; Codex, and Claude Code without Ultracode, keep the original static assignment to a fixed dev-senior sub-agent.

### Removed
- Removed the obsolete legacy C# project, static settings, unit-test project, and accidentally tracked `bin/`/`obj/` artifacts from `main`. They remain recoverable from `legacy-poc-final-2026-07-18` and Git history.
- Removed the obsolete PoC agent-handoff and QA documents from `main` to keep the active documentation focused on the clean implementation. Their history remains available in Git.

### Known limitations
- There is no runnable gateway quick start yet; the repository currently contains the verified solution baseline, research evidence, deterministic SQL Server fixture, and initial technical Catalog Core.
- SQL Server relational-metadata introspection, semantic overlay Markdown/YAML import/validation, and merging the overlay into the technical catalog are all implemented and accepted with real CI evidence. Capability/revision persistence/lifecycle, activation/rollback, and search are not implemented.
- CQM compilation/execution, JSON, OData, MCP, OAuth, administration, packaging, and end-to-end product paths remain pending milestones.

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
