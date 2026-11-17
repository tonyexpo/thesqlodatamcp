# Backlog — thesqlodatamcp

Owner-agreed plan to the handoff's complete **v1**. The product baseline and the v1 definition of done (§29) are in
[AI_DATA_GATEWAY_HANDOFF.md](AI_DATA_GATEWAY_HANDOFF.md); decisions in [decisions.md](decisions.md); current state in
[status.md](status.md). This plan elaborates the handoff, it never reduces it: deferring a v1 requirement needs an
explicit owner scope change.

## v1 endpoint and exclusions

Downloadable Windows/Linux and Docker application; SQL Server reporting source with read-only rights; persistent
SQLite control store; optional semantic import; real MCP OAuth client; entity and analytical queries; equivalent base
OData; CQM `QUERY`/`POST`; administrative revocation; restart without state loss.

**Not in v1:** report builder, raw SQL, query-result persistence/cache, per-user row/column security, other database
providers, scale-out, OData `$apply`/writes/navigation, cloud deployment. TS-08 (ordinal in the structural hash) and
TS-09 (dotted schema names) stay triaged debt unless a real database needs them.

## Step states

`PLANNED` → `IN_PROGRESS` → `IMPLEMENTED_PENDING_QA` → `DONE` (or `DONE — UAT pending`). Also `BLOCKED` (with reason),
`DEFERRED_BY_OWNER`. Effort S/M/L is relative, not a calendar promise.

## Milestone 2 — CQM and SQL Server query engine (in progress)

Milestones 0 (rebaseline) and 1 (catalog foundation) are done; see *Done* below.

### SQL-00 — v1 support matrix · `BLOCKED` (owner decisions)
Record the answers as one ADR ("v1 support matrix"). Recommended defaults in italics:
1. Supported SQL Server versions for the source: *2019, 2022, 2025; CI exercises 2022.*
2. Operating systems/artifacts: *Windows x64 and Linux x64 self-contained + Docker image.*
3. MCP clients proven end to end: *MCP Inspector plus one real desktop client chosen by the owner.*
4. Default exposure: *"visible unless excluded", but a non-empty `DataSource:IncludedSchemas` is required at startup
   (no implicit `dbo`).* Blocks SQL-01a.
5. Public guide: *an administrator-written `publicGuide` overlay field is what `get_reporting_guide` returns; the catalog
   narrative stays internal.* Blocks SQL-01d.

### Checkpoint B — discovery and exposure

| Step | Depends on | Deliverable and acceptance | Effort |
| --- | --- | --- | --- |
| **SQL-01a** | SQL-00 item 4 | `IncludedSchemas`/`ExcludedObjects` applied **in the SQL** (parameterized) by the introspector; FKs whose target is outside the boundary are dropped with a revision warning; options validated at startup. Accept: on the fixture with a second schema and a cross-schema FK, excluded objects are absent from the technical catalog JSON, control store and logs; the cross-schema FK yields a warning; empty `IncludedSchemas` fails validation naming the key. Closes TS-02. | M |
| **SQL-01b** | — | Introspection diagnostics: every projection/type-mapper failure names `schema.object[.column]`; unknown structural metadata (ledger `generated_always_type` 5–8, graph, in-memory, external tables) is skipped with a named warning; inconsistent metadata fails with a named error. Needs a warnings representation on the technical catalog. Accept: ledger column and graph table → catalog built + warnings; corrupt decimal metadata → one error naming the column. Closes TS-05. | M |
| **SQL-01d** | SQL-00 item 5 | `publicGuide` overlay field and `CatalogReadView.PublicGuide`: schema, importer, canonical JSON, hash. The guide states that the server verdict is authoritative over schema-only client validation (TS5-03). Accept: round-trip, hash changes with the field, absent field → `null`; hidden-object names in the guide are the administrator's responsibility (documented). | S |

Checkpoint B acceptance: discovery/exposure probes on the real SQL fixture in CI; no hidden object name in any serializer.

### Checkpoint C — bound identifiers

| Step | Depends on | Deliverable and acceptance | Effort |
| --- | --- | --- | --- |
| **SQL-02a** | Checkpoint B | `ICatalogSnapshot` (immutable `CatalogReadView` + revision id) and `CqmBinder` for `from`, join sources, field references and aliases; unknown/hidden/ambiguous names fail with stable paths. | M |
| **SQL-02b** | 02a | Projection output columns, duplicate/ambiguous alias rules, `orderBy`/`having` alias resolution. | M |
| **SQL-02c** | 02a | One query, one revision: the binder captures the snapshot once; a test activates a new revision between bind and use and proves the old snapshot is used. | S |
| **SQL-02d** | 02a | `page.limit`/`offset` and query limits validated against a `Query:*` options type, reusing `CqmLimits` (absorbs SQL-A6b). | S |

Checkpoint C acceptance: table-driven tests for hidden, unknown, case-different and duplicate names; no source
identifier ever built from request text. **No host code (SQL-07+) starts before checkpoint C.**

### Query engine

| Step | Depends on | Deliverable and acceptance | Effort |
| --- | --- | --- | --- |
| **SQL-03** | SQL-02 | Deterministic expression normalization, operand/result typing, nullability, literal conversions, operator/function/aggregate capabilities, provider-independent errors. Accept: complete vocabulary tables, null semantics, numeric/temporal precision, overflow, division, malformed/deep/large trees; normalization idempotent. | L |
| **SQL-04** | SQL-03 | Joins: explicit `on` > named relationship > automatic only if one unambiguous candidate; inner/left, composite equality, prior-source linkage; reject cross/disconnected/cyclic shapes. Entity vs analytical classification, grouping, aggregates, `having`. Accept: ambiguity errors list candidates; self/composite joins, keyless sources, aggregate nulls, join amplification. | L |
| **SQL-05** | SQL-04 | SQL Server compiler and typed parameter binder: one `SELECT`, bracket-quoted catalog identifiers, deterministic aliases, explicit SQL types for every parameter, no SQL fragments or EF reporting model; total count as a separate controlled operation. Accept: golden tests for the v1 vocabulary; adversarial strings cannot add SQL/batches/comments/writes; compiled examples run on the real fixture with typed expected results. | L |
| **SQL-06** | SQL-05 | Execution: read-only ADO.NET connection, typed reading, cancellation/timeout/concurrency, row/page/byte/join/group/depth/node budgets, `MaxResultBytes` enforced while reading, permit released in `finally`; compact result envelope (§10). Accept: real SQL tests at/below/above limits, cancelled/slow/source-down queries, no literal/data leakage in logs; docs state pagination does not bound scan cost. | L |

## Milestone 3 — Host and JSON API

| Step | Depends on | Deliverable and acceptance | Effort |
| --- | --- | --- | --- |
| **SQL-07** | SQL-01, SQL-06 | ASP.NET host lifecycle, SQLite migrations, bootstrap/introspection/validation/activation/readiness order (§16), fail-safe configuration and secrets. TS-06: forwarded-header allow-list, PublicBaseUrl/TLS validation, empty bootstrap token = admin disabled, MCP `Origin` policy, each with a startup test. Accept: empty/new/old SQLite, restart, invalid bootstrap, clock rollback, source outage/recovery; live ≠ ready; spoofed forwarding cannot alter auth/issuer. No anonymous data exposure. | L |
| **SQL-08** | SQL-06, SQL-07 | JSON/HTTP vertical slice (§12): catalog/entity routes, `QUERY` and `POST` on one handler, `/query/validate` without execution, vendor media type, `Accept-Query`, Problem Details. Deny by default until SQL-09/10. Accept: negotiation/method/body/unknown-property errors, hidden metadata denial, real HTTP tests on the host. | M |

## Milestone 4 — OAuth and backoffice

| Step | Depends on | Deliverable and acceptance | Effort |
| --- | --- | --- | --- |
| **SQL-09** | SQL-07 | Persist OpenIddict state, crypto/data-protection keys and hashed one-time approval tokens; control store holds no query results. Accept: restart keeps state without plaintext secrets; backup/restore with key material rehearsed. | M |
| **SQL-10** | SQL-09 | Standalone OAuth: discovery/protected-resource metadata, capped/rate-limited public DCR, authorization code + PKCE, resource indicators, reference tokens, refresh, client/admin revocation. Accept: a real client authorizes; wrong PKCE/resource/redirect, replay, expiry and registration abuse fail; revocation effective on the next request and after restart; anonymous access denied. | L |
| **SQL-11** | SQL-01, 07, 09, 10 | Protected minimal backoffice (§15): separate admin login, secure short sessions, CSRF/same-site/brute-force defenses; status/source test, approval tokens, OAuth revocation, catalog upload/validate/activate/recover/refresh/export, sanitized audit. Accept: browser tests for unauthorized/CSRF/expired sessions, invalid uploads, last-valid recovery, revoke → immediate denial. No raw SQL box or raw token listing. | L |

## Milestone 5 — OData and MCP adapters

| Step | Depends on | Deliverable and acceptance | Effort |
| --- | --- | --- | --- |
| **SQL-12** | SQL-04, 06, 08, 10 | OData v1: runtime EDM, service document/CSDL, tables and keyed views, key/collection routes, select/filter/orderby/top/skip/count, server paging; published capability matrix; unsupported features fail explicitly. Accept: real OData client results match JSON CQM; hidden objects absent; Power BI tested where practical. | L |
| **SQL-13** | SQL-06, 08, 10 | MCP Streamable HTTP: `get_reporting_guide`, `search_catalog`, `describe_entities`, `get_query_capabilities`, `query_data`, `aggregate_data`, `validate_query`; strict schemas and structured content; Markdown returned inertly. Accept: initialize/list/call/errors, auth/Origin denial, reconnect/restart, real OAuth MCP client + Inspector. | L |
| **SQL-14** | SQL-08, 10, 12, 13 | Cross-adapter equivalence corpus (JSON POST/QUERY, MCP, OData) and least-privilege proof with a database account that cannot write. | M |

## Milestone 6 — Hardening and v1 release

| Step | Depends on | Deliverable and acceptance | Effort |
| --- | --- | --- | --- |
| **SQL-15** | SQL-11, 14 | Threat model (§20), supply chain/secrets/errors review, bounded concurrent load, SQLite/crypto recovery, disk-full/permission/restart tests; triage TS-05/08/09/10 on evidence. Logs exclude tokens/literals/results/Markdown. | L |
| **SQL-16** | SQL-15 | Docker/Compose and self-contained Windows/Linux artifacts, sample DB/catalog/config, install/OAuth/catalog-authoring/backup/restore/upgrade/troubleshooting guides. Accept: clean installs and upgrades from real artifacts; restore to another host. | L |
| **SQL-17** | SQL-16 + all | RC: frozen artifact, complete suites, client/security matrix, capability docs, release notes, final adversarial review. Accept: every §29 row evidenced (table below); owner authorizes tag and release. | M |

### Handoff §29 acceptance crosswalk

| User outcome | Steps | Required proof |
| --- | --- | --- |
| Download release/container | SQL-16/17 | Install the frozen published artifact |
| Configure SQL Server + persistent SQLite | SQL-01/07/16 | New host, persisted volume, no source writes |
| Import optional Markdown/YAML | SQL-01/07/11 | Both input shapes and the no-overlay case |
| Start the app | SQL-07/16 | Readiness/liveness and failed-source behaviour |
| Authorize a real MCP client via OAuth | SQL-09/10/13 | Real client flow, not a mock token |
| Discover/inspect the catalog | SQL-01/08/12/13 | Hidden-object denial; type/key/relationship metadata |
| Entity and analytical queries without SQL | SQL-02–06/13 | Real fixture golden outputs and injection negatives |
| Equivalent base OData | SQL-12/14 | Client plus shared equivalence corpus |
| JSON `QUERY` or `POST` | SQL-08/14 | Both methods, same handler/result |
| Revoke a client in the backoffice | SQL-10/11/14 | Immediate denial, persistent revocation |
| Restart without losing OAuth/catalog | SQL-07/09/10/16 | SQLite/keys restored, valid and revoked states |
| Source cannot write; docs accurate | SQL-14–17 | Least-privilege proof, reviewed docs |
| Unsupported OData explicit; real clients | SQL-12–17 | Capability matrix and interoperability logs |
| Windows/Linux downloads work | SQL-16/17 | Clean-machine artifact smoke per OS |

## Post-v1

Direction from the handoff: v2 richer relationships/OData navigation, explain, external OIDC, SQL Server control store,
multi-replica foundations; v3 OData analytics, Entra ID, managed identity, keyset paging, Power BI hardening; v4
PostgreSQL/MySQL and read-only GraphQL preview; v5 ODBC dialects incl. Databricks/Spark SQL; v6 mature OData 4.01
profile and stable extension contracts. Not pulled into v1 without an owner scope change.

## UAT — Milestone 2

None pending (Milestone 2 has no owner-only checks).

## Done

| Item | Outcome |
| --- | --- |
| Milestone 0 | Rebaseline from the handoff, naming/licence (ADR 0001), library spikes, solution scaffold, CI (2026-07-21). |
| Milestone 1 | Technical catalog, SQL Server type mapping and introspection, semantic overlay import/merge, revisions, SQLite persistence, activation/bootstrap, search (ADR 0006–0015, 2026-09-09). |
| QA repair gates 2026-11-02 | TS-03/04/07, TS2-01/02, TS3-01/02/03 fixed; `CatalogReadView` boundary. |
| CQM v1 structural slice | Strict DTOs, published JSON Schema, structural validator (ADR 0016, accepted 2026-11-04). |
| Phase A / A′ | SQL-A1–A4 input boundaries (request guard, linear expression validation, per-thread schemas, overlay crash safety); SQL7-01…04 fixes; SQL-A5 child-process probes; SQL-A6a bounded error lists. |
| SQL-01c | Public catalog boundary test (`CatalogBoundaryTests`). |

## Carried over

Open deferred findings only, under the step that must handle them first: ID · severity · origin · SHA · description.

- **SQL-01a:** TS-02 · P1 · QA 2026-11-02 · `dbe98fb` · `IncludedSchemas`/`ExcludedObjects` must be SQL-side introspector
  options with defined cross-boundary FK behaviour, validated at startup, before any public catalog/query hosting.
- **SQL-01b:** TS-05 · P2 · QA 2026-11-02 · `dbe98fb` · unusual metadata diagnostics (see SQL-01b).
- **SQL-07:** TS-06 · P1 · QA 2026-11-02 · `dbe98fb` · proxy trust list, empty admin token denial, MCP Origin policy and
  startup validation before host/OAuth/admin acceptance.
- **SQL-08/12/13:** TS-01 wiring · P1 · QA 2026-11-02 · `dbe98fb` · wire `CatalogReadView` into every public catalog and
  query surface (boundary already enforced by SQL-01c).
- **SQL-04:** REL-NAME · P2 · ADR 0011 audit 2026-08-22 · — · an overlay relationship may share its `Name` with an
  FK-discovered one (dedup by name + provenance); named-relationship join resolution needs an explicit rule first.
- **SQL-07:** ADR-0014-LIM · P2 · ADR 0014 · — · `GetActiveAsync` materializes every activated revision's JSON with no
  retention; `CatalogBootstrapCoordinator.RunAsync` assumes a single caller. Revisit before wiring into the host.
- **SQL-10:** DCR · P2 · ADR 0003 spike · — · OpenIddict does not implement RFC 7591 dynamic client registration: design
  and security-test a bounded registration endpoint (redirect URI, client type, rate, resource validation).
- **SQL-15:** TS-08/09/10 · P3 · QA 2026-11-02 · `dbe98fb` · ordinal hash policy, dotted identifiers, supply-chain
  automation — triaged design debt, reopen only on real evidence.
