# ADR 0003 — Protocol, identity, and catalog library baseline

- **Status:** Accepted
- **Date:** 2026-07-19

## Context

Milestone 0 requires executable proof of the current .NET 10 APIs for MCP Streamable HTTP, runtime OData EDM, standalone OAuth, and strict Markdown/YAML catalog validation before the production solution is scaffolded.

The proofs live under `spikes/` and deliberately exercise library seams rather than product features.

## Decision

Use these initial production baselines when scaffolding the solution:

- `ModelContextProtocol.AspNetCore` 1.4.1 for MCP Streamable HTTP. The spike verifies initialization, protocol negotiation, `tools/list`, generated output schema, `tools/call`, and structured content.
- `Microsoft.AspNetCore.OData` 9.5.0 for the OData adapter. The spike verifies a manually constructed runtime `EdmModel`, service document, XML metadata, CLR type annotation, and an entity-set query without an EF reporting-source model.
- `OpenIddict.Server.AspNetCore` 7.6.0 for standalone OAuth. The API proof covers authorization code, global PKCE, refresh, revocation, reference tokens, registered resource indicators, and per-client resource permissions.
- `Markdig` 0.42.0, `YamlDotNet` 16.3.0, and `JsonSchema.Net` 7.3.0 for combined Markdown/front-matter extraction, typed camel-case YAML deserialization, and versioned strict schema validation.

Keep every protocol adapter behind the CQM. In particular, the OData spike's direct query handling proves library compatibility only and must not become a production query bypass.

OpenIddict does not provide OAuth Dynamic Client Registration. Before Milestone 5 implementation, design and test a bounded RFC 7591 registration endpoint backed by OpenIddict's application manager, or adopt a separately validated component. Do not weaken registration limits, redirect-URI validation, public-client policy, or resource validation to compensate.

## Consequences

- Pin these versions centrally when the production projects are scaffolded; upgrades require rerunning the relevant spike and regression tests.
- Keep the spikes outside the production solution and do not copy their sample architecture blindly.
- Track Dynamic Client Registration as application-owned security work rather than an assumed OpenIddict feature.
- Select SQL Server disposable-test infrastructure separately because its real container gate had not yet run successfully when this decision was accepted.

## Subsequent evidence

[ADR 0004](./0004-sqlserver-test-infrastructure.md) later accepted the pinned Testcontainers infrastructure after the owned-container path passed on the intended GitHub Actions runner.
