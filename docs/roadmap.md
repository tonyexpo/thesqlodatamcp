# Roadmap

## How to read this roadmap

The [authoritative handoff](./AI_DATA_GATEWAY_HANDOFF.md) defines the product and six-release direction. This roadmap turns its recommended v1 implementation order into delivery milestones with explicit exit gates. The [backlog](./backlog.md) contains the actionable checklist.

Milestones are dependency-ordered. A milestone is complete only when its exit gate is demonstrated by tests or documentation; compilation alone is not completion.

## Milestone 0 — Rebaseline and de-risk

**Outcome:** a clean, named, licensed solution baseline whose critical library choices have been proven with small executable spikes.

**Status:** Completed on 2026-07-21.

Product naming, repository continuity, licensing, legacy-code disposition, .NET identifier casing, the initial library baselines, production solution scaffolding, configuration conventions, build policy, baseline tests, and CI workflow are settled. GitHub Actions run [29778536859](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/29778536859) demonstrated both the production validation job and the dependent owned-Testcontainers SQL Server integration job on the intended runner.

**Exit gate:** the target solution builds in CI; the selected packages have minimal working spikes; no production feature depends on an obsolete preview or an assumed API; legacy code is clearly isolated or scheduled for removal.

## Milestone 1 — Catalog foundation

**Outcome:** SQL Server can be introspected into a stable technical catalog and safely merged with validated semantic documentation.

**Status:** In progress. The provider-neutral technical catalog model, deterministic structural hash, and SQL Server catalog type mapper are accepted. The first SQL Server introspection slice is implemented locally and awaiting its real disposable-database CI gate; relational metadata and the remaining catalog lifecycle are pending.

Work includes catalog domain types, canonical/provider type mapping, SQL Server introspection, Markdown/YAML parsing and validation, relationship resolution, SQLite control-store migrations, revision activation/rollback, and in-memory search indexes.

**Exit gate:** integration tests discover representative SQL Server schemas; valid overlays activate atomically; invalid refreshes preserve the last valid revision; catalog search and serialization are deterministic.

## Milestone 2 — CQM and safe SQL compiler

**Outcome:** the gateway can accept, validate, compile, and execute the full v1 Canonical Query Model without any raw-SQL escape hatch.

Work includes strict CQM DTOs/schema, expression normalization, type inference, validation codes/paths, join priority, entity and analytical query classification, SQL Server compilation, explicit parameters, pagination, result envelopes, cancellation, timeouts, concurrency and size limits.

**Exit gate:** golden tests prove one `SELECT`, catalog-only identifiers, no inline literals, deterministic aliases, and correct parameters; disposable-SQL integration tests cover entity queries, aggregates, explicit joins, limits, timeout, cancellation, and paging.

## Milestone 3 — JSON vertical slice and operations baseline

**Outcome:** the query core is usable end-to-end through a versioned HTTP API in a controlled development environment.

Work includes ASP.NET Core hosting, catalog endpoints, `QUERY` and `POST`, query validation, Problem Details, health checks, readiness behavior, structured safe logging, configuration validation, sample database/catalog, and Docker development topology.

**Exit gate:** an integration client can discover the catalog and run equivalent entity and analytical queries through JSON; protocol errors are stable; source outages and invalid configuration produce documented health/error behavior. This milestone is not a public release and must not expose unauthenticated production data.

## Milestone 4 — OData and MCP adapters

**Outcome:** base OData and MCP capabilities are thin, tested adapters over the same CQM and catalog.

Work includes the OData v1 service document, runtime metadata, keyed entity sets, filtering/projection/sorting/paging/count, MCP Streamable HTTP, the seven v1 MCP tools, structured tool schemas/content, and cross-adapter equivalence tests.

**Exit gate:** MCP Inspector plus a real MCP client can initialize, list, and call tools; a real OData client can query supported features; equivalent JSON/MCP/OData requests produce equivalent logical queries; unsupported OData behavior is explicit.

## Milestone 5 — OAuth, control plane, and administration

**Outcome:** real clients can authorize, refresh, and revoke access, while an administrator can manage the minimum required operational state.

Work includes OpenIddict authorization code + PKCE, discovery, protected-resource metadata, dynamic registration controls, reference tokens, refresh/revocation, approval tokens, bootstrap admin access, protected backoffice, catalog upload/activation, revocation UI, antiforgery, rate limits, persistent keys, and minimal admin audit.

**Exit gate:** unauthenticated catalog/query access is impossible; the OAuth lifecycle works end-to-end with a real MCP client; administrative revocation takes effect on the next request; restart preserves OAuth and catalog state; security tests cover abuse and leakage paths.

## Milestone 6 — v1 hardening and release

**Outcome:** a downloadable, documented, reproducible public v1 satisfying the handoff definition of done.

Work includes threat-model closure, performance and security regression testing, Power BI validation where practical, Docker and self-contained Windows/Linux artifacts, deployment/auth guides, OData capability matrix, samples, CI releases, upgrade/backup notes, and public-repository hygiene.

**Exit gate:** every item in the handoff's v1 definition of done is demonstrated; the source database requires no write permission; real MCP and OData clients pass; a clean installation and restart work from released artifacts; security documentation matches behavior.

## Post-v1 release direction

- **v2:** richer relationships and OData navigation, explain capability, external OIDC, SQL Server control store, multi-replica foundations.
- **v3:** OData analytics, Entra ID, managed identity/service principal, possible keyset paging, Power BI hardening.
- **v4:** native PostgreSQL/MySQL and read-only GraphQL preview.
- **v5:** explicit ODBC dialects including Databricks/Spark SQL, GraphQL analytics, multi-provider hardening.
- **v6:** mature documented OData 4.01 profile, stable GraphQL/provider extension contracts, broad conformance and operational maturity.
