# Development state

**Checkpoint date:** 2026-07-19

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

## QA evidence at this checkpoint

- MCP restore/build: passed, zero warnings; `spikes/mcp-sdk/verify.sh`: passed end-to-end.
- OData tests: 3 passed, 0 failed.
- OpenIddict tests: 2 passed, 0 failed.
- Catalog validation tests: 4 passed, 0 failed.
- SQL Server Testcontainers project: restore/build passed, zero warnings.
- `dotnet format --verify-no-changes`: passed for all five spike projects.
- `git diff --check`: passed before checkpoint finalization.

The sandbox blocks local sockets by default, so VSTest, formatter, and MCP loopback verification required authorized execution. That is an environment constraint, not a product failure.

## Open gates and risks

### Disposable SQL Server test

ADR 0004 remains Proposed. `Testcontainers.MsSql` 4.8.1 and `Microsoft.Data.SqlClient` 6.1.1 compile, and the test pins `mcr.microsoft.com/mssql/server:2022-CU25-ubuntu-22.04`, but the real test cannot start because `/var/run/docker.sock` returns `permission denied`, including during authorized execution.

Do not mark the backlog item complete until the same test:

1. starts the pinned container;
2. opens a real SQL connection;
3. executes the typed parameterized `SELECT`;
4. passes on the intended CI runner;
5. demonstrates cleanup behavior.

### Dynamic Client Registration

OpenIddict 7.6.0 does not implement RFC 7591 Dynamic Client Registration. Before Milestone 5, design and security-test a bounded registration endpoint backed by OpenIddict's application manager, or validate a dedicated component. Do not disable redirect-URI, client-type, registration-rate, or resource validation as a shortcut.

## Next dependency-ordered work

Start Phase 0.3, delegated to `gpt-5.6-terra` and reviewed by the primary agent:

1. scaffold `thesqlodatamcp.slnx` with the five source and four test projects from ADR 0002;
2. establish only the dependency directions justified by `docs/architecture.md`;
3. add central package/version management using ADR 0003 baselines;
4. add nullable analysis, warnings-as-errors, analyzers, formatting, deterministic builds, and shared build properties;
5. establish example configuration and local-secret ignore conventions;
6. add CI for restore, build, unit tests, formatting, and documentation links;
7. run the SQL Server Docker test on a capable local/CI environment and accept or revise ADR 0004.

Do not start Milestone 1 catalog production implementation until the remaining Milestone 0 exit criteria are demonstrated.

## Restart checklist

1. Run `git status --short` and `git log -1 --oneline`.
2. Read `AGENTS.md`, the project skill, this file, ADR 0002–0004, architecture, roadmap, and backlog.
3. Confirm whether Docker access is now available before retrying the SQL Server gate.
4. Re-run affected spike tests if SDK/runtime/package state changed.
5. Create a bounded Phase 0.3 delegation plan and retain final QA ownership.
