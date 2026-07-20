# Development state

**Checkpoint date:** 2026-07-20

**Branch:** `main`

**Milestone:** 0 — Rebaseline and de-risk

This file is the restart point when conversational context is unavailable. Read it after `AGENTS.md` and the project skill.

## Operating model

- The primary Codex agent is the software architect and QA lead.
- Production implementation is delegated to a `gpt-5.6-terra` sub-agent with bounded scope and acceptance criteria.
- The primary agent owns architecture, review, automated-test adequacy, final validation, ADRs, backlog, changelog, and this checkpoint.
- The canonical project skill is `skills/thesqlodatamcp-technical-lead/SKILL.md`.
- The current runtime mounts repository-local `.codex` and `.agents` as read-only tmpfs directories. `AGENTS.md` therefore points to the version-controlled skill under `skills/`; a personal installed copy may also exist for automatic discovery.

## Completed and accepted

### Phase 0.1 — .NET identity

- Root .NET identifier and namespace: `TheSqlODataMcp`.
- Solution filename: `thesqlodatamcp.slnx`.
- Five source-project and four test-project names are fixed by ADR 0002.

### Phase 0.2 — validated library seams

- MCP: `ModelContextProtocol.AspNetCore` 1.4.1 on .NET 10. The spike proves Streamable HTTP initialization, version negotiation, `tools/list`, generated output schema, `tools/call`, and structured content.
- OData: `Microsoft.AspNetCore.OData` 9.5.0. The spike proves one controller pipeline bound to a manual runtime EDM, service document, XML metadata, CLR type annotation, and `$orderby`, without an EF reporting-source model.
- OAuth: `OpenIddict.Server.AspNetCore` 7.6.0. The API proof covers authorization code, PKCE, refresh, revocation, reference tokens, registered resource indicators, and per-client resource permissions.
- Catalog input: `Markdig` 0.42.0, `YamlDotNet` 16.3.0 with camel-case naming, and `JsonSchema.Net` 7.3.0. Positive and negative tests prove real Markdown front-matter extraction and strict unknown-property rejection.
- Obsolete PoC source and historical PoC QA/agent-handoff documents are absent from `main` and remain recoverable from Git history and `legacy-poc-final-2026-07-18`.

Accepted choices are recorded in ADR 0002 and ADR 0003. The spikes are executable evidence, not production architecture, and must remain outside the production solution.

### Phase 0.3 — production solution baseline

- `thesqlodatamcp.slnx` contains exactly the five source and four test projects from ADR 0002; spike projects remain outside it.
- Dependency direction is fixed and tested: Core has no product dependency; SqlServer, Persistence, and Protocols depend on Core; Web composes all four boundaries.
- `global.json`, central package management, shared nullable/warnings/analyzer/formatting policy, and deterministic build settings are active.
- ADR 0003 packages are pinned centrally and placed by architectural boundary. `Microsoft.Data.SqlClient` 6.1.1 uses the compiled spike baseline; no unselected EF Core/control-store package was introduced.
- The tracked application configuration follows the handoff sections with blank sensitive values; local overrides and secret files are ignored.
- CI is defined for restore, build, all four test projects, formatting, and offline local Markdown-link validation.

The accepted baseline is recorded in ADR 0005. The first successful execution on the intended GitHub Actions runner remains a Milestone 0 gate.

### Phase 0.4 — deterministic SQL Server fixture candidate

- `tests/fixtures/reporting-catalog/contract.json` defines a versioned provider-neutral catalog contract for future provider equivalence.
- The initial SQL Server implementation recreates the fixed `TheSqlODataMcp_TestCatalog` database and deterministically seeds 8,128 rows across twelve tables.
- Fixture complexity covers seven schemas, simple/composite keys, composite and ambiguous foreign-key paths, a compatible legacy-code join without an FK, hierarchy, nullable values, unique/filtered indexes, constraints, persisted computed columns, rowversion, temporal metadata, broad scalar/provider types, keyless views, and extended descriptions.
- Inert procedure, function, trigger, sequence, synonym, and table-type objects exist so future introspection tests can prove their exclusion.
- The spike uses either an externally supplied SQL Server connection or an owned pinned Testcontainers instance, executes scripts through `Microsoft.Data.SqlClient`, and always drops only the fixed fixture database in `finally`.
- A separate CI job is prepared to run the complete spike on an Ubuntu runner with Docker.
- `spikes/Directory.Packages.props` keeps explicit research-spike package pins independent from production Central Package Management and resolves the Phase 0.3 `NU1008` restore regression.

The fixture design, scripts, static tests, and harness are accepted repository infrastructure. The real SQL Server path and ADR 0004 remain unaccepted until the local/external and intended CI evidence passes.

## QA evidence at this checkpoint

- MCP restore/build: passed, zero warnings; `spikes/mcp-sdk/verify.sh`: passed end-to-end.
- OData tests: 3 passed, 0 failed.
- OpenIddict tests: 2 passed, 0 failed.
- Catalog validation tests: 4 passed, 0 failed.
- Enhanced SQL Server fixture spike: restore/build passed with zero warnings; 3 static contract and batch-parser tests passed.
- All other spike projects restore successfully with the spike-local package-management override.
- `dotnet format --verify-no-changes`: passed for all five spike projects.
- `git diff --check`: passed before checkpoint finalization.
- Production solution restore: passed.
- Production solution build: passed, zero warnings and zero errors.
- Production baseline tests: 6 passed, 0 failed, 0 skipped across all four test projects.
- Production `dotnet format --verify-no-changes`: passed.
- Offline repository-local Markdown link validation: passed.
- Production solution inventory and dependency/package placement checks: passed.

The sandbox blocks local sockets by default, so VSTest, formatter, and MCP loopback verification required authorized execution. That is an environment constraint, not a product failure.

The Phase 0.3 build, VSTest, and formatter checks were therefore independently rerun with authorized execution. The ordinary sandbox failure was caused by denied MSBuild/VSTest named-pipe sockets, not by compilation or test failures.

## Open gates and risks

### First CI runner execution

The GitHub Actions workflow is present and its commands pass locally, but it has not yet run on the intended remote runner. Keep the CI backlog item open until the baseline job and the new SQL Server integration job both pass there.

### Disposable SQL Server test

ADR 0004 remains Proposed. `Testcontainers.MsSql` 4.8.1 and `Microsoft.Data.SqlClient` 6.1.1 compile, and the test pins `mcr.microsoft.com/mssql/server:2022-CU25-ubuntu-22.04`, but the real test cannot start here because `/var/run/docker.sock` returns `permission denied`. No `THESQLODATAMCP_TEST_SQLSERVER_CONNECTION_STRING` is currently configured.

Do not mark the backlog item complete until the same test:

1. starts the pinned container or reaches the explicitly configured external test server;
2. recreates and validates the deterministic fixture contract;
3. opens a real SQL connection and executes the typed parameterized `SELECT`;
4. drops the fixture database and proves it is absent;
5. passes through the owned-container path on the intended CI runner.

### Dynamic Client Registration

OpenIddict 7.6.0 does not implement RFC 7591 Dynamic Client Registration. Before Milestone 5, design and security-test a bounded registration endpoint backed by OpenIddict's application manager, or validate a dedicated component. Do not disable redirect-URI, client-type, registration-rate, or resource validation as a shortcut.

## Next dependency-ordered work

Close the remaining Milestone 0 gates in this order:

1. run the full fixture integration test against the SQL Server container supplied by the project owner through `THESQLODATAMCP_TEST_SQLSERVER_CONNECTION_STRING`;
2. verify deterministic bootstrap, metadata/data assertions, typed parameterized execution, and database removal;
3. push the pending commits and run both jobs on the intended GitHub Actions runner, including the owned-Testcontainers path;
4. accept or revise ADR 0004 from that evidence;
5. confirm every Milestone 0 exit criterion before planning the bounded first slice of Milestone 1.

Do not start Milestone 1 catalog production implementation until the remaining Milestone 0 exit criteria are demonstrated.

## Restart checklist

1. Run `git status --short` and `git log -1 --oneline`.
2. Read `AGENTS.md`, the project skill, this file, ADR 0002–0005, architecture, roadmap, and backlog.
3. Confirm whether Docker access is now available before retrying the SQL Server gate.
4. Re-run affected spike tests if SDK/runtime/package state changed.
5. Inspect the first GitHub Actions run and the SQL Server Docker gate before declaring Milestone 0 complete.
