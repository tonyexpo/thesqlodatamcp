# AI Data Gateway

> Working title. The final product and repository name are still to be chosen.

AI Data Gateway is a planned self-hosted, read-only data gateway for AI assistants, agents, OData clients, Power BI, and ordinary HTTP clients.

The target product exposes relational reporting data through:

- MCP over Streamable HTTP;
- a documented read-only OData 4.01 profile;
- a versioned JSON query API;
- HTTP `QUERY`, with `POST` as the compatibility fallback.

All protocol adapters translate into one typed Canonical Query Model (CQM). Callers never submit SQL. The SQL Server provider resolves catalog identifiers, emits one parameterized `SELECT`, and executes with a least-privilege read-only database identity.

## Repository status

This repository previously contained a legacy proof of concept named `thesqlodatamcp`. That implementation was not a secure or interoperable foundation and has been removed from `main`. Its final committed state is preserved by the annotated tag `legacy-poc-final-2026-07-18` and by Git history.

The project is now being rebaselined from the authoritative AI Data Gateway handoff. The target architecture is a new .NET 10 / ASP.NET Core solution rather than an incremental extension of the stdio/raw-DQL prototype.

Implementation has not restarted yet. The next work is the remainder of Milestone 0: confirm final product naming, validate implementation-time library choices, and scaffold the new solution and CI baseline.

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
- [Project status and agent handoff](./docs/project-status-handoff.md) — legacy assessment and transition from Qwen 3.6 35B to Codex 5.6 Terra.
- [Architecture](./docs/architecture.md) — concise target architecture and legacy disposition.
- [Roadmap](./docs/roadmap.md) — ordered v1 milestones, dependencies, and exit gates.
- [Backlog](./docs/backlog.md) — actionable implementation checklist.
- [Claude QA analysis](./docs/qa-analysis-claude.md) — historical QA of the legacy proof of concept.
- [Changelog](./docs/changelog.md)

## Current implementation

There is intentionally no quick start yet. A downloadable working v1 is the first release target; its acceptance criteria are defined in the authoritative handoff.

License: [Apache License 2.0](./LICENSE).
