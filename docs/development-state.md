# Development state

**Checkpoint date:** 2026-07-21

**Branch:** `main`

**Milestone:** 1 — Catalog foundation (in progress)

This file is the restart point when conversational context is unavailable. Read it after `AGENTS.md` and the project skill.

## Operating model

- The primary Codex agent is the software architect and QA lead.
- Production implementation is delegated to a `gpt-5.6-terra` sub-agent with bounded scope and acceptance criteria.
- The primary agent owns architecture, review, automated-test adequacy, final validation, ADRs, backlog, changelog, and this checkpoint.
- The canonical project skill is `skills/thesqlodatamcp-technical-lead/SKILL.md`.
- Repository-local `.codex` and `.agents` may be mounted read-only; the version-controlled project skill remains canonical.

## Session checkpoint — 2026-07-21

Milestone 0 is complete. Commit `54a31dd` is present on both local `main` and `origin/main`. GitHub Actions run [29778536859](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/29778536859) passed on the intended Ubuntu runner:

- `validate` passed restore, warning-free build, all production tests, formatting, and offline Markdown-link validation;
- the dependent `sqlserver-integration` job passed Docker discovery, restore, warning-free spike build, static fixture tests, and the real owned-Testcontainers SQL Server test;
- the real test bootstrapped and validated the deterministic database, executed an explicitly typed parameterized query, dropped the fixed database, and proved it absent.

ADR 0004 is therefore Accepted, and the Milestone 0 CI and disposable SQL Server backlog items are closed. Local agent sandboxes can still deny `/var/run/docker.sock`; this no longer blocks the accepted CI infrastructure.

The first bounded Milestone 1 slice is implemented and validated as a local snapshot. Production implementation was delegated to `gpt-5.6-terra`; the primary agent reviewed it, required corrections, added independent QA tests, and ran the final validation. Keep this snapshot local until the project owner chooses to push it.

## Completed and accepted

### Milestone 0 — Rebaseline and de-risk

- Project identity, Apache License 2.0, repository continuity, and legacy-PoC preservation are fixed by ADR 0001.
- .NET solution/project/namespace naming is fixed by ADR 0002.
- MCP, OData, OpenIddict, Markdown/YAML, and JSON Schema library seams are fixed by ADR 0003 and their executable spikes.
- Testcontainers SQL Server infrastructure is fixed by ADR 0004 and its successful intended-runner evidence.
- The production solution, dependency graph, package placement, shared build policy, configuration conventions, and CI baseline are fixed by ADR 0005.
- `thesqlodatamcp.slnx` contains exactly five source projects and four test projects; spikes remain outside it.

### Milestone 1 slice 1 — provider-neutral technical Catalog Core

ADR 0006 records the accepted initial technical-catalog contract:

- stable physical identities preserve schema/object casing and use ordinal comparison;
- tables, keyed views, and keyless views are representable without synthetic keys;
- the exact v1 canonical scalar vocabulary is retained alongside inert provider type name, store representation, length, precision, and scale;
- fields, primary/alternate keys, useful indexes including filtered indexes, relationships, and ordered relationship field pairs are modeled;
- identity, computed, persisted-computed, temporal-period, rowversion, and temporal-entity metadata have construction-time invariants;
- input collections are defensively copied and duplicate or missing local field references are rejected;
- canonical camel-case JSON sorts unordered entity/named metadata with ordinal semantics while preserving meaningful key/index/pair order;
- lowercase SHA-256 structural hashes exclude timestamps and environment-dependent values.

The slice deliberately does not implement SQL Server introspection, semantic overlays, capabilities, revision persistence/lifecycle, search, CQM, or protocol behavior.

## QA evidence at this checkpoint

### Remote Milestone 0 evidence

- GitHub Actions run `29778536859`: success.
- `validate`: success.
- `sqlserver-integration`: success through the owned pinned Testcontainers path.

### Local Catalog Core evidence

- `dotnet restore thesqlodatamcp.slnx`: passed; all projects up to date.
- `dotnet build thesqlodatamcp.slnx --no-restore`: passed with zero warnings and zero errors.
- `dotnet test tests/TheSqlODataMcp.Core.Tests/TheSqlODataMcp.Core.Tests.csproj --no-build --no-restore`: 12 passed, 0 failed, 0 skipped.
- `dotnet test thesqlodatamcp.slnx --no-build --no-restore`: 17 passed, 0 failed, 0 skipped across all four production test projects.
- `dotnet format thesqlodatamcp.slnx --verify-no-changes --no-restore`: passed.
- Independent QA covers ordinal/case-preserving identity, provider-detail hash sensitivity, lowercase SHA-256 shape, invalid enums, duplicate ordinals, and multiple primary keys in addition to the delegated positive and negative cases.

The ordinary sandbox denied VSTest sockets and Roslyn/MSBuild pipes. Those commands were independently rerun with authorized execution and passed. This remains an environment constraint rather than a product defect.

## Open work and risks

### SQL Server catalog type mapping and introspection

No production SQL Server introspector exists yet. The next slice must map the fixture's provider types into the accepted canonical vocabulary, query SQL Server metadata through `Microsoft.Data.SqlClient`, construct the Core catalog, and exclude system/programmable objects by construction.

The real integration test must cover the accepted fixture, including tables/views, simple/composite keys, useful indexes, ambiguous/composite/self foreign keys, identity/computed/temporal/rowversion metadata, extended descriptions, keyless views, broad scalar types, and exclusions.

### Catalog lifecycle remains pending

Semantic Markdown/YAML merge, strict structural validation, capability models, SQLite revision persistence, atomic activation/rollback, bootstrap modes, and in-memory search are not implemented. Do not mark the remaining Milestone 1 backlog items complete.

### Dynamic Client Registration

OpenIddict 7.6.0 does not implement RFC 7591 Dynamic Client Registration. Before Milestone 5, design and security-test a bounded registration endpoint backed by OpenIddict's application manager, or validate a dedicated component. Do not weaken redirect-URI, client-type, registration-rate, or resource validation.

## Next dependency-ordered work

1. Keep the validated Catalog Core snapshot local until the project owner chooses to push it.
2. Define the bounded SQL Server type-mapping and introspection contract against ADR 0006 and the deterministic reporting fixture.
3. Implement canonical mapping for the fixture's supported SQL Server scalar types, with explicit `unknown` behavior and unit tests.
4. Implement metadata discovery for user schemas, tables/views, columns, keys, useful indexes, foreign keys, computed/temporal flags, and descriptions.
5. Exclude system schemas and unsupported procedures, functions, triggers, sequences, synonyms, table types, and internal objects by construction.
6. Run the production introspector against the real disposable SQL Server fixture in CI and compare the resulting catalog to deterministic expectations.
7. Only after introspection is accepted, proceed to semantic Markdown/YAML merge and strict validation; capability and revision lifecycle models should be introduced with their first production consumers.

## Restart checklist

1. Run `git status --short --branch` and inspect the local commits and complete diff; Codex does not push automatically.
2. Read ADR 0006 and the Catalog Core implementation/tests before extending the model.
3. Re-run production restore, build, tests, formatting, Markdown-link validation, and `git diff --check` after any change.
4. Use the deterministic SQL Server fixture for introspection work; do not replace the real provider path with mocks or build-only evidence.
5. Preserve the Core dependency direction and never introduce SQL fragments, provider client types, semantic rules, or protocol concerns into the technical catalog domain.
