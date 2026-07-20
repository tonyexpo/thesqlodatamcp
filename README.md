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

The project is now being rebaselined from the authoritative AI Data Gateway handoff. The target architecture is a new .NET 10 / ASP.NET Core solution rather than an incremental extension of the stdio/raw-DQL prototype.

Implementation has restarted with isolated Milestone 0 research spikes and a production solution baseline. The nine-project .NET 10 solution, dependency directions, central package versions, strict build policy, safe example configuration, baseline tests, and CI workflow are now present and locally validated. A provider-neutral reporting-catalog contract and deterministic SQL Server bootstrap/teardown fixture are also prepared. The first run on the intended GitHub Actions runner and the real SQL Server Docker/external-server gate are still required before Milestone 0 can close.

## Product boundaries

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
- [ADR 0004: SQL Server test infrastructure](./docs/decisions/0004-sqlserver-test-infrastructure.md) — proposed Testcontainers baseline and its remaining acceptance gate.
- [ADR 0005: solution, build, and CI baseline](./docs/decisions/0005-solution-build-and-ci-baseline.md) — production project graph, package placement, shared build policy, configuration, tests, and CI.
- [Architecture](./docs/architecture.md) — concise target architecture and legacy disposition.
- [Roadmap](./docs/roadmap.md) — ordered v1 milestones, dependencies, and exit gates.
- [Backlog](./docs/backlog.md) — actionable implementation checklist.
- [Development state](./docs/development-state.md) — verified restart checkpoint, open gates, and next dependency-ordered work.
- [Deterministic reporting-catalog fixture](./tests/fixtures/reporting-catalog/README.md) — portable logical contract plus SQL Server bootstrap and teardown assets.
- [Changelog](./docs/changelog.md)

## Current implementation

There is intentionally no quick start yet. A downloadable working v1 is the first release target; its acceptance criteria are defined in the authoritative handoff.

License: [Apache License 2.0](./LICENSE).
