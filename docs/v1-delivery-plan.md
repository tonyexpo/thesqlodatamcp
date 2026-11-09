# v1 delivery plan — architecture, development slices and QA gates

Updated **2026-11-09**, reviewed baseline **`4df04ac`**. Execution breakdown by the QA lead/software architecture lead.
The [authoritative handoff](AI_DATA_GATEWAY_HANDOFF.md), accepted [ADRs](decisions/), [architecture](architecture.md),
[roadmap](roadmap.md) and [backlog](backlog.md) remain authoritative in that order. This file elaborates their steps;
it does **not** replace them or silently reduce the handoff's v1 definition of done (§29).

## Baseline, endpoint and ownership

Milestones 0/1 and the structural CQM slice exist. Compiler, execution, protocols, standalone OAuth, backoffice and
packaging **do not**. A catalog/parser acceptance is not a runnable-gateway release. See the
[latest QA](qa-check-2026-11-09T14-47-28Z.md) for exact-source evidence and limitations.

The v1 endpoint is unchanged: downloadable Windows/Linux and Docker application, SQL Server reporting source with
read-only rights, persistent SQLite, optional semantic import, actual MCP OAuth client, entity/analytical queries,
equivalent base OData, CQM `QUERY`/`POST`, administrative revocation and restart without state loss. No report builder,
raw SQL, query-result persistence/cache, per-user row/column security, extra database provider or scale-out.

The primary QA/architecture lead owns contracts/dependencies/acceptance; implementation assignment continues to obey
`AGENTS.md` and the canonical technical-lead skill. The owner approves product changes/release. No production work is
implemented or automatically delegated by this document. All steps below are **open release work/evidence**, even
where a component is already partially present.

## Dependency-ordered development steps

### SQL-00 — Accept the foundation and freeze the v1 matrix
- **Input:** current QA, ADR 0016 (structural slice independently Accepted on 2026-11-04), handoff §§6–8, 29. Record exact SHA evidence and remaining non-blocking debt.
- **Deliver:** accepted structural contract after review, supported OS/SQL/client versions, capabilities vocabulary,
  version/error compatibility rules and explicit implementation-ticket boundaries. Do not upgrade an ADR on build alone.
- **Gate:** all previous TS3 reproductions closed independently; state remaining catalog/proxy/auth debt and link it
  to steps below. Existing catalog acceptance remains historical, not a waiver of future security gates.

### SQL-01 — Configuration-driven technical discovery and safe public catalog
- **Depends:** SQL-00. Implement/validate `IncludedSchemas`/`ExcludedObjects` (TS-02), SQL-side filtering and policy for
  FKs crossing the discovery boundary. Do not make default fixed metadata SQL caller-controlled.
- **Deliver:** one supported catalog read-view boundary and stable external identity resolution; public adapters and
  resolver must use it. Include hidden relationships, physical FKs, keyless views, dotted/quoted identifiers and drift.
- Bound combined front matter **before** Markdown parsing, not only inside the common importer. Reconcile the stripped
  public metadata narrative with mandatory `get_reporting_guide`: define the administrator-approved public guide path,
  not a silent loss of the handoff's guide feature or an invented automatic prose-redaction promise.
- **Gate:** real SQL+SQLite tests prove excluded objects cannot be discovered/publicly found and invalid refresh
  preserves active revision; exposed-only serializers cannot reach hidden physical metadata. Query-denial through
  aliases/relationships is carried forward into SQL-02/14 once an executable query path exists.

### SQL-02 — Resolve CQM identifiers against one immutable revision
- **Depends:** SQL-01. Resolve sources/fields/aliases/projections against an exposed catalog snapshot; produce a bound
  provider-neutral representation, output columns and stable diagnostic paths. Avoid holding mutable global catalog state.
- **Gate:** unknown/hidden/ambiguous names, case and duplicate aliases fail consistently; one query uses one revision
  through validation/execution even if refresh activates another. No source identifier comes from unchecked request text.

### SQL-03 — Normalize expressions and infer canonical types
- **Depends:** SQL-02. Implement deterministic normalization, operand/result typing, nullability, literal conversions,
  operator/function/aggregate capability decisions and provider-independent errors (§6). No silent coercion of bad values.
- **Gate:** table-driven complete vocabulary tests, null semantics, numeric/temporal precision, overflow, divisions,
  malformed/deep/large trees; normalization idempotence and equivalent ASTs produce identical bound intent.

### SQL-04 — Resolve joins and analytical semantics
- **Depends:** SQL-03. Explicit `on` overrides FK; named relationship next; automatic only if one unambiguous candidate.
  Support inner/left, composite equality conjunctions, prior-source linkage; reject cross/disconnected/cyclic unsafe shapes.
- Implement entity/analytical classification, grouping equivalence, selected non-aggregate/group-by completeness,
  aggregate/type compatibility, alias availability and `having` rules.
- **Gate:** ambiguity includes useful candidates; same-name overlay overrides work; self/composite joins, keyless sources,
  aggregate nulls and join amplification tests. A structural equality alone is not proof of a resolved safe join.

### SQL-05 — SQL Server compiler and typed parameter binder
- **Depends:** SQL-04. Implement provider interfaces with their first consumers; no speculative provider framework.
  Exactly one data `SELECT` per compiled operation, bracket-quoted catalog identifiers, deterministic generated aliases,
  explicit SQL types/length/precision/scale for every value parameter; no SQL fragment or EF reporting-source model.
- Cover projection/filter/calculation/joins/group/having/order/distinct/offset paging/count semantics and supported
  functions/aggregates. Define total-count as a separate controlled operation when required, never a multi-batch escape.
- **Gate:** golden tests cover the v1 vocabulary; adversarial every-string inputs cannot introduce SQL/batches/comments/
  writes. Execute compiled examples on the real deterministic fixture and compare typed expected results.

### SQL-06 — Execution, resource limits and result envelope
- **Depends:** SQL-05. ADO.NET read-only connection, typed binding/reading, cancellation/timeout/concurrency, row/page/byte/
  join/group/aggregate/depth/node budgets, deterministic paging warnings, no query sessions/jobs/cache.
- **Deliver:** columns once + compact row arrays, stable order/type metadata, nextOffset, truncation/count/duration/
  revision/warnings per §10; no semantically ambiguous success after a hard safety limit (§19).
- **Gate:** actual SQL tests at/below/above limits, cancelled/slow/source-down queries, result-byte enforcement during
  reading, permit release after exceptions and no leakage of literals/data in logs. Pagination does not bound scan cost.

### SQL-07 — Persistent host lifecycle, startup validation and health
- **Depends:** SQL-01/06. Build ASP.NET host, SQLite migrations, active-revision load/bootstrap/introspection/validation/
  activation/search/readiness order (§16), fail-safe configuration and secret sources; source-down admin/live availability.
- Resolve TS-06 proxy trust allow-list, forwarded-header order, PublicBaseUrl/TLS validation, empty bootstrap-token denial
  and MCP Origin policy. Never expose anonymous data while authentication is still being implemented.
- **Gate:** empty/new/old SQLite, restart, invalid bootstrap/clock rollback/source outage/recovery; live and ready differ;
  spoofed forwarding cannot alter auth/issuer semantics; private key/DB permissions are validated/documented.

### SQL-08 — Shared JSON/HTTP QUERY vertical slice
- **Depends:** SQL-06/07; deny-by-default until SQL-09/10. Implement exact §12 routes: catalog/entity, query via QUERY
  and POST (one handler), validate without execution; vendor/application JSON, Accept-Query and stable Problem Details.
- **Gate:** content negotiation/method/body/unknown-property errors, hidden metadata denial, parameterized entity and
  analytical result envelope, validate proves no database execution. Real HTTP tests use the deployed artifact/host.

### SQL-09 — Persist OAuth, keys and approval-token state
- **Depends:** SQL-07. Add supported OpenIddict EF entities, crypto/data-protection persistence and migrations, hashed
  approval tokens (display once, expiry/revocation/use audit); control DB stores no query results or report definitions.
- **Gate:** restart preserves necessary state without exposing plaintext secrets; tokens are not direct data credentials;
  backup/restore with key material is rehearsed. Do not treat a process-local development signing key as production-ready.

### SQL-10 — Standalone OAuth and administrative revocation
- **Depends:** SQL-09. Discovery/protected-resource metadata, capped/rate-limited public DCR, authorization code + PKCE,
  required resource indicators, reference access tokens, refresh and client/admin token/grant/client/session revocation.
- **Gate:** a real compatible client authorizes; wrong/missing PKCE/resource/redirect, replay, expiry and registration
  abuse fail; revocation takes effect on the next request and persists through restart. Anonymous catalog/query denied.
  An external OIDC integration is not a substitute for the mandatory standalone v1 contract.

### SQL-11 — Protected minimal backoffice
- **Depends:** SQL-01/07/09/10. Separate admin login/bootstrap secret, short secure cookie sessions, CSRF/same-site/
  brute-force defenses; status/source test, approval tokens, OAuth inspection/revocation, catalog upload/validate/
  activate/recover/refresh/export and minimal sanitized audit (§15).
- **Gate:** browser tests for unauthorized/CSRF/expired sessions, invalid/oversize uploads, last-valid recovery, one-time
  token display, revoke then immediate data denial. No raw SQL text box, raw token listing or arbitrary server-file access.

### SQL-12 — OData v1 metadata and read-only query adapter
- **Depends:** SQL-04/06/08/10. Runtime EDM without reporting EF; service document/XML CSDL, tables and keyed views,
  collection/key routes, select/filter/order/top/skip/count, supported functions/nulls/server paging translated into CQM.
- Publish the exact capability matrix. Keyless views require declared logical keys for OData, not synthesized keys;
  unsupported writes/expand/navigation/apply/batch fail explicitly, not silently ignored.
- **Gate:** real HTTP/OData client and fixture results match equivalent JSON CQM, including keys/types/null/paging;
  hidden objects cannot appear in metadata or data. Power BI base interoperability is tested where practical, gaps recorded.

### SQL-13 — MCP Streamable HTTP and catalog/query façades
- **Depends:** SQL-06/08/10. Current supported SDK; guide (and useful resource), search_catalog, describe_entities,
  get_query_capabilities, query_data, aggregate_data, validate_query. Strict input/output schemas and structured content.
- Separate entity/analytical façades over the same engine; semantic Markdown is data, never a tool/system instruction.
- **Gate:** initialize/list/call/errors, schema-conforming results, prompt-injection prose returned inertly, validation
  without execution, auth/Origin denial, reconnect/restart and actual OAuth MCP client + Inspector evidence.

### SQL-14 — Cross-adapter equivalence and least-privilege proof
- **Depends:** SQL-08/10/12/13. One corpus of canonical cases across JSON POST/QUERY, MCP and the OData v1 intersection.
- **Gate:** equal rows/types/nulls/filtering/paging/error semantics and one catalog revision; database account with
  no write rights executes supported queries and demonstrably cannot write or run unsupported commands. No privileged
  developer SA account is used as the evidence for production least privilege.

### SQL-15 — Security, reliability and operational envelope
- **Depends:** SQL-11/14. Complete threat model (§20), supply-chain/secret/error review, bounded concurrent load,
  cold/warm SQL behavior, SQLite/crypto recovery and disk-full/denied-permission/restart tests.
- Triage old TS-05/08/09/10 against real evidence, including diagnostics, ordinal hashing, dotted identifiers and action/
  dependency automation. No old hypothesis is called a reproduced vulnerability merely to fill a checklist.
- **Gate:** no unaccepted blocker; safe configured defaults are demonstrated, not inferred from page size; logs/errors
  exclude tokens/literals/results/Markdown. Single-instance SQLite remains explicit; no unapproved multi-replica support.

### SQL-16 — Packaging, samples and clean-machine operations
- **Depends:** SQL-15. Docker/Compose and self-contained Windows/Linux artifacts, sample source DB/catalog/config,
  install/config/OAuth/catalog-authoring/deployment/backup/restore/upgrade/troubleshooting guides; persistent SQLite volume.
- **Gate:** fresh installs and upgrades work from actual artifacts, not repo-only scripts; documented TLS/proxy setup,
  source/SQLite/key permission requirements, offline/failed source behavior and restore to another host demonstrated.
  No cloud deployment is implied or performed without approval.

### SQL-17 — Independent RC acceptance and v1 publication
- **Depends:** SQL-16 and every preceding mandatory gate. Freeze artifact SHA/version; run complete suites and client/
  security matrix, publish supported OData/CQM capabilities, known limitations and release notes; request independent QA.
- **Gate:** every §29 row below has evidence; no unaccepted critical/high/medium safety defect; owner authorizes tag and
  downloadable release. A successful partial CI, placeholder host or skipped real-client path does not close v1.

## Mandatory handoff acceptance crosswalk (not yet accepted)

| Handoff §29 user outcome | Steps | Required proof |
| --- | --- | --- |
| Download release/container | SQL-16/17 | Install the frozen published artifact |
| Configure SQL Server + persistent SQLite | SQL-01/07/16 | New host, persisted volume, no source writes |
| Import optional Markdown/YAML | SQL-01/07/11 | Both input shapes + no-overlay case |
| Start app | SQL-07/16 | Readiness/liveness and failed-source behavior |
| Authorize real MCP client via OAuth | SQL-09/10/13 | Client flow, not a mock token only |
| Discover/inspect catalog | SQL-01/08/12/13 | Hidden-denial and type/key/relationship metadata |
| Entity and analytical queries without SQL | SQL-02–06/13 | Real fixture golden outputs and injection negatives |
| Equivalent base OData | SQL-12/14 | Client plus shared equivalence corpus |
| JSON QUERY or POST | SQL-08/14 | Both methods, same handler/result |
| Revoke client in backoffice | SQL-10/11/14 | Immediate denial, persistent revocation |
| Restart without losing OAuth/catalog | SQL-07/09/10/16 | SQLite/keys restored, valid and revoked states |
| All tests / source cannot write / accurate security docs | SQL-14–17 | Full gates, least-privilege proof, reviewed docs |
| Unsupported OData explicit / real SQL and clients | SQL-12–17 | Capability matrix and interoperability logs |
| Windows/Linux downloads work | SQL-16/17 | Clean-machine artifact smoke, per OS |

## Progress and future adversarial reviews

Track per ID: TODO / IN_PROGRESS / BLOCKED / IMPLEMENTED_PENDING_QA / ACCEPTED / DEFERRED_BY_OWNER, dependencies,
implementation SHA, exact commands/counts, CI URL, negative cases, risk disposition and independent report. Only
ACCEPTED closes a mandatory gate; deferring an existing v1 requirement needs explicit owner amendment of scope.

Each new QA states its baseline and affected step IDs, rechecks previous findings, attacks input/limits/auth boundaries
and validates documentation claims against working artifacts. Keep historical reports; do not rewrite a timeout as
a pass after a diagnostic retry. Milestone-level QA is requested, not an uncontrolled fix/review loop.

**Next development ticket:** checkpoint A′ is accepted by the 2026-11-09 independent QA (SQL7-01…04 closed;
SQL-A5/A6a and SQL-01c verified). **SQL-01b** is next while SQL-00 decisions 4/5 are pending, then SQL-01a/01d and
SQL-02 (discovery/exposure and bound identifiers) before type inference/compiler. This document and the QA live on
`main-qa`; no host or compiler feature is implied.
