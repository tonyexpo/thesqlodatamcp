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
- [Status](./docs/status.md) ([owner view](./docs/status.html)) — current position, gates, open findings and owner decisions.
- [Backlog](./docs/backlog.md) — milestones, SQL-00–17 steps with acceptance criteria and the handoff §29 crosswalk.
- [Decisions](./docs/decisions.md) — decision log indexing the [ADRs](./docs/decisions/0001-project-identity.md).
- [Canonical Query Model v1](./docs/cqm.md) — public query contract, expression forms, and validation boundary.
- [Architecture](./docs/architecture.md) — concise target architecture and legacy disposition.
- [QA status](./docs/qa-status.md) — latest adversarial QA per step and deferred findings.
- [Deterministic reporting-catalog fixture](./tests/fixtures/reporting-catalog/README.md) — portable logical contract plus SQL Server bootstrap and teardown assets.
- [Changelog](./docs/changelog.md)
- [Agent workflow](./AGENTS.md) — development process, gates and product boundaries for development chats.

## Current implementation

There is intentionally no quick start yet. A downloadable working v1 is the first release target; its acceptance criteria are defined in the authoritative handoff.

License: [Apache License 2.0](./LICENSE).
