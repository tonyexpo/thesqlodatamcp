# Backlog

## Source of truth

The [AI Data Gateway handoff](./AI_DATA_GATEWAY_HANDOFF.md) is the authoritative product baseline. The [roadmap](./roadmap.md) defines milestone order and exit gates. This file tracks implementation work; it must not silently change settled product boundaries.

## Milestone 0 — Rebaseline and de-risk

### Documentation and decisions

- [x] Import the complete original AI Data Gateway handoff into `docs/` without modification.
- [x] Reconcile README, architecture, roadmap, backlog, project status, and changelog with the handoff.
- [x] Preserve the obsolete PoC handoff and QA record in Git history and the historical tag; remove them from `main`.
- [x] Set `thesqlodatamcp` as the definitive public product and repository name.
- [x] Select the corresponding .NET solution/project/assembly/namespace casing during scaffolding.
- [x] Confirm Apache License 2.0 as the final license.
- [x] Retain and rebaseline this existing public Git repository.
- [x] Preserve the final PoC as `legacy-poc-final-2026-07-18` and remove obsolete source/tests from `main`.
- [x] Record project identity, repository continuity, historical preservation, and licensing in ADR 0001.
- [ ] Record subsequent settled implementation choices as short ADRs.

### Implementation-time research spikes

- [x] Verify the current supported official .NET MCP SDK with Streamable HTTP and structured tool output.
- [x] Verify ASP.NET Core OData compatibility with .NET 10 and runtime EDM generation without a reporting-source EF model.
- [x] Verify current OpenIddict flows for PKCE, dynamic client registration, resource indicators, reference tokens, refresh, and revocation; record that RFC 7591 registration requires application-owned implementation or another validated component.
- [x] Select JSON Schema and YAML/front-matter libraries after strict-validation prototypes.
- [ ] Select disposable SQL Server integration-test infrastructure that works locally and in CI.

### Solution baseline

- [ ] Scaffold the five target source projects and four test projects without further fragmentation.
- [ ] Add central package/version management, nullable analysis, formatting, analyzers, and deterministic builds.
- [ ] Establish CI for restore, build, unit tests, and documentation/link checks.
- [x] Remove legacy tracked `bin/`/`obj/` artifacts and ignore local agent state.
- [ ] Establish the new example-configuration and local-secret ignore convention when the solution is scaffolded.

## Milestone 1 — Catalog foundation

- [ ] Define technical catalog, entity, field, key, relationship, type, capability, and revision models.
- [ ] Implement SQL Server schema/table/view/column/key/index/FK/computed/temporal/description introspection.
- [ ] Exclude system and unsupported programmable objects by construction.
- [ ] Implement canonical SQL type mapping with preserved provider details.
- [ ] Implement Markdown plus YAML/front-matter import and the v1 structured schema.
- [ ] Reject forbidden semantic sections and invalid physical references.
- [ ] Implement merge precedence, FK/configured relationships, keyless-view rules, and deterministic hashes.
- [ ] Add SQLite control-store migrations and catalog revision persistence.
- [ ] Implement atomic activation, last-valid rollback behavior, bootstrap modes, and in-memory catalog/search indexes.
- [ ] Cover catalog parsing, merging, revisions, drift, and real SQL Server introspection with tests.

## Milestone 2 — CQM and SQL Server query engine

- [ ] Define versioned strict CQM DTOs and a published JSON Schema.
- [ ] Implement expression normalization, canonical scalar types, type inference, aliases, and stable validation errors/paths.
- [ ] Implement entity/analytical classification, grouping rules, aggregate rules, and v1 operator/function capabilities.
- [ ] Implement explicit/named/automatic join resolution with ambiguity errors and v1 join limits.
- [ ] Implement provider abstractions and SQL Server catalog-only identifier resolution/quoting.
- [ ] Compile exactly one parameterized `SELECT`; expose no SQL-fragment or raw-query escape hatch.
- [ ] Implement typed parameter binding, deterministic aliases, offset paging, stable-order warnings, and total-count behavior.
- [ ] Implement compact result envelopes, cancellation, timeout, concurrency, row, byte, join, aggregate, and expression limits.
- [ ] Add golden compiler tests and disposable-SQL integration tests for the full v1 query surface.
- [ ] Add security regression tests for injection through every external string, query amplification, and accidental writes.

## Milestone 3 — JSON API and operational baseline

- [ ] Build the ASP.NET Core host, configuration validation, forwarded-header ordering, and safe secret sources.
- [ ] Implement versioned catalog/entity endpoints.
- [ ] Implement shared HTTP `QUERY` and `POST` execution plus `POST /query/validate`.
- [ ] Implement the versioned vendor media type, `Accept-Query`, strict JSON parsing, and Problem Details codes/paths.
- [ ] Implement liveness/readiness, source-down behavior, correlation IDs, safe structured logs, and metrics baseline.
- [ ] Add sample SQL database/seed/catalog and Docker Compose development setup.
- [ ] Add JSON protocol and end-to-end integration tests.

## Milestone 4 — OData and MCP adapters

### OData v1

- [ ] Publish the read-only OData capability matrix.
- [ ] Implement service document and runtime XML CSDL metadata.
- [ ] Expose tables and keyed views; exclude keyless views unless YAML provides a logical key.
- [ ] Map GET by key/collection, `$select`, `$filter`, `$orderby`, `$top`, `$skip`, `$count`, null handling, core functions, and server paging to CQM.
- [ ] Add real-client and Power BI compatibility tests where practical.

### MCP v1

- [ ] Host MCP over Streamable HTTP using the validated current SDK.
- [ ] Implement `get_reporting_guide`, `search_catalog`, `describe_entities`, and `get_query_capabilities`.
- [ ] Implement `query_data`, `aggregate_data`, and `validate_query` as CQM façades.
- [ ] Publish strict input/output schemas and conforming structured content.
- [ ] Ensure semantic Markdown is returned as data, not injected into tool descriptions or server instructions.
- [ ] Add MCP initialize/list/call/error tests, MCP Inspector coverage, and a real-client E2E.
- [ ] Add cross-adapter equivalence tests across JSON, OData, and MCP.

## Milestone 5 — OAuth, persistence, and backoffice

- [ ] Implement standalone OpenIddict OAuth discovery/metadata and authorization code + PKCE.
- [ ] Implement capped/rate-limited dynamic public-client registration and required resource indicators.
- [ ] Implement reference access tokens, refresh tokens, client revocation, and administrative token/grant/client/session revocation.
- [ ] Implement hashed, one-time-displayed approval tokens that never grant direct data access.
- [ ] Persist OpenIddict state and cryptographic/data-protection material safely in the control store.
- [ ] Implement separate bootstrap admin authentication and the protected minimal backoffice.
- [ ] Support connection status/test, OAuth administration, approval tokens, catalog validation/activation/rollback/refresh/export, and minimal audit.
- [ ] Apply secure cookies, antiforgery, same-site rules, short sessions, rate limits, constant-time comparisons, and upload limits.
- [ ] Prove anonymous denial, PKCE, refresh, revocation, restart persistence, CSRF protection, and log/error redaction in tests.

## Milestone 6 — v1 hardening and public release

- [ ] Complete and review the threat model against implementation and tests.
- [ ] Verify source-database least privilege and inability to write independently of application validation.
- [ ] Validate default limits under query-amplification and concurrency pressure.
- [ ] Produce Docker, Docker Compose, and self-contained Windows/Linux artifacts.
- [ ] Write deployment, OAuth, configuration, catalog-authoring, backup/restore, and troubleshooting guides.
- [ ] Add security policy, contribution guide, issue templates, OData matrix, samples, automated releases, and upgrade notes.
- [ ] Execute MCP Inspector, real MCP client, real OData client, Power BI where practical, Docker, persistence, and clean-install E2E scenarios.
- [ ] Demonstrate every item in the handoff v1 definition of done before tagging v1.

## Post-v1

Post-v1 scope follows the six-release roadmap in the authoritative handoff. Do not pull v2+ features into v1 unless they remove a proven blocker or the project owner explicitly changes scope.
