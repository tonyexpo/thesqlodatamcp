# Backlog — SQL OData MCP Connector

## Current status

The current code is an incomplete proof of concept. A successful build and the 13 existing unit tests do not validate MCP tool discovery, MCP interoperability, SQL behavior, authentication, or a safe read-only boundary. See [Project Status & Handoff](./project-status-handoff.md) for the verified state and the agent transition record.

No new product feature should be implemented until Phase 0 is completed and the prior project analysis from the owner's other computer has been added to this repository.

## Phase 0 — Recovery and project design baseline

- [x] Record the public project handoff from Qwen 3.6 35B to Codex 5.6 Terra.
- [x] Document the current proof-of-concept limitations and known security issues.
- [ ] Add the owner's existing project-analysis documentation to `docs/` when it is available.
- [ ] Reconcile that analysis with this backlog, architecture document, and product scope.
- [ ] Define target users and deployment modes: local stdio only, remote HTTP, or both.
- [ ] Define supported SQL Server versions, network topology, tenancy assumptions, and the data-classification model.
- [ ] Produce a threat model, including agent trust, credential storage, SQL injection, data exfiltration, denial of service, and audit requirements.
- [ ] Define explicit v1 non-goals; in particular, decide whether arbitrary SQL text is out of scope.

## Phase 1 — Security and identity architecture

- [ ] Require a dedicated database identity with least-privilege `SELECT` permissions, preferably against approved views rather than arbitrary base tables.
- [ ] Define allowlists for schemas, entities/views, columns, and optionally row-level policies.
- [ ] Choose authentication per transport:
  - [ ] For stdio, define local process/client trust and credential ownership; do not pretend a configured bearer token is request authentication.
  - [ ] For future remote HTTP transport, design real OAuth 2.1/OIDC integration, token validation, authorization policies, and audience/scopes.
- [ ] Replace the tracked runtime `settings.json` pattern with a safe template/example and ignored local secrets.
- [ ] Fix settings schema alignment and fail fast when required configuration is missing.
- [ ] Define structured logging, secret redaction, audit events, rate limits, query timeouts, cancellation, maximum rows, and maximum response size.

## Phase 2 — Common query contract

- [ ] Design a transport-neutral, typed read-query contract over approved entities/views.
- [ ] Define projections, typed comparisons, boolean filter composition, sorting, pagination/cursors, aggregate support, and explicit limits.
- [ ] Define stable request/response DTOs and error semantics.
- [ ] Decide whether joins, aggregates, and subqueries are supported; if so, specify them structurally rather than accepting raw SQL.
- [ ] Define metadata discovery: schemas, primary keys, relationships, descriptions, and data types.
- [ ] Map the contract deliberately to OData query semantics and document any unsupported OData features.
- [ ] Reserve a versioned mapping for the future ATP JSON-query interface.

## Phase 3 — MCP server baseline

- [ ] Decide whether to remain on the pinned preview SDK or upgrade after validating its real API and compatibility policy.
- [ ] Make tool discovery operational (`[McpToolType]`, method attributes, dependency injection) and verify it with an MCP client.
- [ ] Keep MCP stdio stdout protocol-only; send diagnostics to an appropriate logger/stderr without secrets.
- [ ] Replace ad-hoc string and tuple tool results with serializable DTOs and documented schemas.
- [ ] Define the v1 MCP tool list from the common query contract, including metadata and paged read tools.
- [ ] Add protocol-level integration tests for initialize, list-tools, invalid input, authorization behavior, and tool invocation.

## Phase 4 — SQL Server implementation

- [ ] Implement the contract using parameterized ADO.NET commands with explicit SQL types and sizes.
- [ ] Quote/validate all identifiers through allowlists, never through caller-provided bracket interpolation.
- [ ] Remove the free-form SQL `WHERE` input. If raw SQL is ever retained, use a real parser/AST and treat it as a separately reviewed capability.
- [ ] Enforce query limits, timeouts, cancellation, deterministic ordering for pagination, and structured output.
- [ ] Add SQL Server integration tests for permissions, schema discovery, filtering, paging, limits, failures, and cancellation.
- [ ] Add regression tests for `SELECT INTO`, `WAITFOR`, identifier escaping, comment handling, and all other security findings.

## Phase 5 — OData adapter

- [ ] Select hosting and OData implementation technology after the common query contract is stable.
- [ ] Expose approved entities, metadata, filtering, sorting, and paging through an intentionally limited OData surface.
- [ ] Verify interoperability with the target Power BI usage scenarios.
- [ ] Apply the same authorization, limits, auditing, and error model as the MCP path.

## Phase 6 — ATP JSON-query adapter

- [ ] Define ATP's exact meaning, API boundary, JSON request schema, response schema, versioning, and authorization model.
- [ ] Map only approved portions of ATP JSON queries to the common query contract.
- [ ] Add compatibility, validation, security, and performance tests.

## Delivery and documentation

- [ ] Remove already tracked `bin/` and `obj/` test artifacts from Git while preserving local build output.
- [ ] Establish CI for formatting, build, unit tests, integration tests, security regression tests, and dependency review.
- [ ] Keep architecture, changelog, threat model, API contract, and deployment guidance aligned with each implementation milestone.
- [ ] Define release criteria: reviewed threat model, end-to-end MCP test, SQL least privilege verification, documented OAuth/transport behavior, and successful target-client validation.
