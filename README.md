# thesqlodatamcp

> A self-hosted, read-only data gateway for AI assistants, agents, OData clients, Power BI, and ordinary HTTP clients.

`thesqlodatamcp` is the definitive product and repository name. The imported architecture handoff uses “AI Data Gateway” as its original working title; [ADR 0001](./docs/decisions/0001-project-identity.md) records the subsequent naming decision.

The target product exposes relational reporting data through:

- MCP over Streamable HTTP;
- a documented read-only OData 4.01 profile;
- a versioned JSON query API;
- HTTP `QUERY`, with `POST` as the compatibility fallback.

All protocol adapters translate into one typed Canonical Query Model (CQM). Callers never submit SQL. The SQL Server provider resolves catalog identifiers, emits one parameterized `SELECT`, and executes with a least-privilege read-only database identity.

## Repository status

This repository previously contained a legacy proof of concept named `thesqlodatamcp`. That implementation was not a secure or interoperable foundation and has been removed from `main`. Its final committed state is preserved by the annotated tag `legacy-poc-final-2026-07-18` and by Git history.

The project has been rebaselined from the authoritative AI Data Gateway handoff. Implementation now proceeds in the new .NET 10 / ASP.NET Core solution rather than as an incremental extension of the stdio/raw-DQL prototype.

Milestone 0 is complete: the .NET 10 solution, dependency directions, central package versions, strict build policy, safe example configuration, baseline tests, CI workflow, and deterministic SQL Server fixture have passed on the intended GitHub Actions runner. Milestone 1 is complete as well: the accepted slices provide the provider-neutral technical Catalog Core, deterministic structural hashing, strict SQL Server catalog type mapping, complete table/view/column/key/index/foreign-key introspection, semantic Markdown/YAML overlay import and validation, merging the overlay into the technical catalog, the revision lifecycle, SQLite persistence, atomic activation/rollback and bootstrap modes, JSON deserialization, and the in-memory search index. The full catalog pipeline is covered by a real SQL Server and SQLite integration test. Milestone 2 is in progress: the versioned CQM v1 DTOs, strict JSON Schema, parser/serializer, and structural validator are implemented; catalog resolution, type inference, normalization, SQL compilation/execution, and the HTTP host remain pending, so there is still no runnable gateway.

## Product boundaries

**Latest QA (2026-11-04):** [independent verification](./docs/qa-check-2026-11-04T09-54-31Z.md) of published `2f63e58`
closes TS3-01/02/03 with 359 full-suite passes (including two real SQL integration tests), three fixture-static tests,
format/audit/links and exact-source CI green. Acceptance is **scoped to the fix/structural foundation**, not v1.
The [SQL-00–17 delivery plan](./docs/v1-delivery-plan.md) maps remaining work to every handoff acceptance outcome.
New QA/planning is on `main-qa`; there is still no runnable gateway.

- Read-only by construction; no writes or arbitrary commands.
- No caller-supplied or LLM-generated raw SQL.
- Runtime database introspection; no static ORM model for reporting data.
- EF Core is permitted only for the internal control store.
- One versioned CQM shared by MCP, OData, JSON, and future protocols.
- A small stable MCP tool surface, never one tool per table.
- Explicit capabilities and stable machine-readable validation errors.

The gateway is a catalog and query layer, not a BI dashboard builder, metric registry, report store, chart engine, or file-export service.

## Documentation

- [Authoritative project handoff](./docs/AI_DATA_GATEWAY_HANDOFF.md) — settled product boundaries, contracts, security model, release sequence, and v1 definition of done.
- [ADR 0001: project identity](./docs/decisions/0001-project-identity.md) — definitive name, repository continuity, historical tag, and license.
- [ADR 0002: .NET identifiers](./docs/decisions/0002-dotnet-identifiers.md) — solution, project, assembly, and namespace casing.
- [ADR 0003: initial library baseline](./docs/decisions/0003-protocol-identity-catalog-libraries.md) — validated MCP, OData, OpenIddict, and catalog parsing choices.
- [ADR 0004: SQL Server test infrastructure](./docs/decisions/0004-sqlserver-test-infrastructure.md) — accepted pinned Testcontainers baseline and real-runner evidence.
- [ADR 0005: solution, build, and CI baseline](./docs/decisions/0005-solution-build-and-ci-baseline.md) — production project graph, package placement, shared build policy, configuration, tests, and CI.
- [ADR 0006: technical Catalog Core](./docs/decisions/0006-technical-catalog-core-model.md) — provider-neutral catalog representation, invariants, canonical JSON, and structural hashing.
- [ADR 0007: SQL Server type mapping](./docs/decisions/0007-sqlserver-type-mapping.md) — provider-boundary scalar mapping, metadata validation, and explicit unknown behavior.
- [ADR 0008: SQL Server introspection foundation](./docs/decisions/0008-sqlserver-introspection-foundation.md) — accepted table/view/column discovery boundary and real-fixture regression gate.
- [ADR 0009: SQL Server relational metadata introspection](./docs/decisions/0009-sqlserver-relational-metadata-introspection.md) — accepted key/index/foreign-key discovery boundary, verified against the real SQL Server fixture.
- [ADR 0010: semantic overlay import and validation](./docs/decisions/0010-semantic-overlay-import-validation.md) — accepted Markdown/YAML overlay import, dual-strictness validation, and physical-reference checking.
- [ADR 0011: merged catalog](./docs/decisions/0011-merged-catalog.md) — accepted merge precedence, discovered/configured relationship union, and keyless-view key rules.
- [ADR 0012: catalog revision lifecycle](./docs/decisions/0012-catalog-revision-lifecycle.md) — accepted immutable, timestamped, hash-addressed catalog build snapshots.
- [ADR 0013: catalog revision persistence](./docs/decisions/0013-catalog-revision-persistence.md) — accepted SQLite/EF Core control-store persistence for catalog revisions.
- [ADR 0014: catalog activation and bootstrap modes](./docs/decisions/0014-catalog-activation-and-bootstrap-modes.md) — accepted atomic activation, last-valid rollback, and startup bootstrap policy.
- [ADR 0015: catalog deserialization and search index](./docs/decisions/0015-catalog-deserialization-and-search-index.md) — accepted JSON-to-domain reconstruction and in-memory catalog search.
- [ADR 0016: CQM v1 contract](./docs/decisions/0016-cqm-v1-contract.md) — accepted bounded query-model/schema/structural-validation slice; compiler and execution remain pending.
- [Canonical Query Model v1](./docs/cqm.md) — public query contract, expression forms, and validation boundary.
- [Architecture](./docs/architecture.md) — concise target architecture and legacy disposition.
- [Roadmap](./docs/roadmap.md) — ordered v1 milestones, dependencies, and exit gates.
- [Backlog](./docs/backlog.md) — actionable implementation checklist.
- [v1 delivery plan](./docs/v1-delivery-plan.md) — dependency-ordered tickets, per-step gates and handoff acceptance crosswalk for development and future QA.
- [Development state](./docs/development-state.md) — verified restart checkpoint, open gates, and next dependency-ordered work.
- [Deterministic reporting-catalog fixture](./tests/fixtures/reporting-catalog/README.md) — portable logical contract plus SQL Server bootstrap and teardown assets.
- [Changelog](./docs/changelog.md)

## Current implementation

There is intentionally no quick start yet. A downloadable working v1 is the first release target; its acceptance criteria are defined in the authoritative handoff.

License: [Apache License 2.0](./LICENSE).
