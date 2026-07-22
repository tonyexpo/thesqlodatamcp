# Development state

**Checkpoint date:** 2026-07-22

**Branch:** `main`

**Milestone:** 1 — Catalog foundation (in progress)

This file is the restart point when conversational context is unavailable. Read it after `AGENTS.md` and the project skill.

## Operating model

- The primary Codex agent is the software architect and QA lead.
- Production implementation is delegated to a `gpt-5.6-terra` sub-agent with bounded scope and acceptance criteria.
- The primary agent owns architecture, review, automated-test adequacy, final validation, ADRs, backlog, changelog, and this checkpoint.
- The canonical project skill is `skills/thesqlodatamcp-technical-lead/SKILL.md`.
- Repository-local `.codex` and `.agents` may be mounted read-only; the version-controlled project skill remains canonical.

## Session checkpoint — 2026-07-22

Milestone 0 is complete. Commit `54a31dd` is present on both local `main` and `origin/main`. GitHub Actions run [29778536859](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/29778536859) passed on the intended Ubuntu runner:

- `validate` passed restore, warning-free build, all production tests, formatting, and offline Markdown-link validation;
- the dependent `sqlserver-integration` job passed Docker discovery, restore, warning-free spike build, static fixture tests, and the real owned-Testcontainers SQL Server test;
- the real test bootstrapped and validated the deterministic database, executed an explicitly typed parameterized query, dropped the fixed database, and proved it absent.

ADR 0004 is therefore Accepted, and the Milestone 0 CI and disposable SQL Server backlog items are closed. Local agent sandboxes can still deny `/var/run/docker.sock`; this no longer blocks the accepted CI infrastructure.

The first two bounded Milestone 1 slices are saved in commits `cd29eeb` (technical Catalog Core) and `3d0cc50` (SQL Server catalog type mapping), both present on `origin/main`. The third local commit contains the SQL Server introspection candidate and remains unpushed.

Production implementation for every slice was delegated to `gpt-5.6-terra`; the primary agent retained architecture and acceptance ownership, reviewed the complete diff, added independent QA, and ran the available validation.

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

### Milestone 1 slice 2 — SQL Server catalog type mapping

ADR 0007 records the accepted provider-boundary mapping contract:

- catalog inputs contain only provider type name, maximum length, precision, and scale; the public mapping API exposes no SQL client types;
- invariant normalization produces deterministic lowercase provider names and store representations;
- supported integral, decimal, floating-point, character, binary, GUID, date/time, and rowversion families map into the ADR 0006 canonical vocabulary;
- Unicode byte lengths, `max` sentinels, decimal storage bands, float storage bands, and all temporal scale/storage bands are handled explicitly;
- meaningful provider length, precision, and scale are retained without introducing provider behavior into Core;
- impossible metadata for known types fails explicitly;
- unsupported, spatial, hierarchical, variant, and user-defined names remain conservative `unknown` values without invented metadata.

This accepted slice contains no catalog SQL, connection handling, discovery, or entity construction. The separately tracked candidate below begins that provider work.

## Candidate under validation

### Milestone 1 slice 3A — SQL Server table/view/column introspection

ADR 0008 is Proposed. The locally committed candidate:

- exposes a connection-string/timeout/cancellation contract without public SQL client types;
- executes one fixed read-only `SELECT` over `sys.objects`, schemas, columns, types, tables, computed columns, and extended properties;
- discovers non-shipped user tables and views while excluding system schemas, temporal history tables, and non-`U`/`V` programmable or auxiliary objects by construction;
- constructs deterministic entities and fields with ordinal casing/order, ADR 0007 type mapping, descriptions, nullability, identity, computed/persisted-computed, temporal-period, and rowversion metadata;
- deliberately leaves keys, indexes, and relationships empty for the next slice;
- includes a production Testcontainers integration test and a dedicated CI route against the fixed SQL Server fixture.

The local environment denies `/var/run/docker.sock` even with authorized execution. Therefore the real production introspector test has not run here and ADR 0008 must remain Proposed until the Docker-capable CI job passes.

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

### Local SQL Server type-mapping evidence

- `dotnet restore thesqlodatamcp.slnx`: passed; all projects up to date.
- `dotnet build thesqlodatamcp.slnx --no-restore`: passed with zero warnings and zero errors.
- `dotnet test tests/TheSqlODataMcp.SqlServer.Tests/TheSqlODataMcp.SqlServer.Tests.csproj --no-build --no-restore`: 76 passed, 0 failed, 0 skipped.
- `dotnet test thesqlodatamcp.slnx --no-build --no-restore`: 92 passed, 0 failed, 0 skipped across all four production test projects.
- `dotnet format thesqlodatamcp.slnx --verify-no-changes --no-restore`: passed.
- `bash eng/verify-markdown-links.sh`: passed.
- `git diff --check`: passed.
- Independent QA covers all temporal scales, every decimal storage boundary, both float storage bands, common invalid metadata for unknown types, null input, unknown-name normalization, and absence of SQL client types from the public mapping API.

### Local introspection-candidate evidence

- `dotnet restore thesqlodatamcp.slnx`: passed; all projects up to date.
- `dotnet build thesqlodatamcp.slnx --no-restore`: passed with zero warnings and zero errors.
- `dotnet test tests/TheSqlODataMcp.SqlServer.Tests/TheSqlODataMcp.SqlServer.Tests.csproj --no-build --no-restore`: 87 passed, 0 failed, 0 skipped.
- `dotnet test tests/TheSqlODataMcp.IntegrationTests/TheSqlODataMcp.IntegrationTests.csproj --no-build --no-restore --filter "Category!=SqlServerIntegration"`: 4 passed, 0 failed, 0 skipped.
- `dotnet test thesqlodatamcp.slnx --no-build --no-restore --filter "Category!=SqlServerIntegration"`: 104 passed, 0 failed, 0 skipped across all four production test projects.
- Independent QA verifies the single fixed read-only statement, structural object filters, declared-user-type `unknown` behavior, deterministic projection, and the real-fixture expectations.
- `docker info`: failed because access to `/var/run/docker.sock` is denied in this agent environment; the same result occurred with authorized execution.
- The `Category=SqlServerIntegration` production test is pending its first run on the intended CI runner and has not been skipped or claimed as passing.

The ordinary sandbox denied VSTest sockets and Roslyn/MSBuild pipes. Those commands were independently rerun with authorized execution and passed. This remains an environment constraint rather than a product defect.

## Open work and risks

### SQL Server catalog introspection

The table/view/column introspection candidate exists but is not accepted until its real CI gate passes. Keys, unique constraints/indexes, filtered indexes, foreign keys, and relationship field pairs are not implemented yet.

The real integration test must cover the accepted fixture, including tables/views, simple/composite keys, useful indexes, ambiguous/composite/self foreign keys, identity/computed/temporal/rowversion metadata, extended descriptions, keyless views, broad scalar types, and exclusions.

### Catalog lifecycle remains pending

Semantic Markdown/YAML merge, strict structural validation, capability models, SQLite revision persistence, atomic activation/rollback, bootstrap modes, and in-memory search are not implemented. Do not mark the remaining Milestone 1 backlog items complete.

### Dynamic Client Registration

OpenIddict 7.6.0 does not implement RFC 7591 Dynamic Client Registration. Before Milestone 5, design and security-test a bounded registration endpoint backed by OpenIddict's application manager, or validate a dedicated component. Do not weaken redirect-URI, client-type, registration-rate, or resource validation.

## Next dependency-ordered work

1. Push the local slice 3A candidate when the project owner chooses, then inspect the dedicated production SQL Server integration job.
2. If the real fixture gate passes, change ADR 0008 to Accepted and record the remote run evidence; otherwise correct the candidate without weakening expectations.
3. Implement the bounded keys, useful indexes, foreign keys, and ordered relationship-pair slice.
4. Re-run the full production introspector against the real fixture and close the introspection/exclusion backlog items only when the complete metadata surface is demonstrated.
5. Only after introspection is accepted, proceed to semantic Markdown/YAML merge and strict validation; capability and revision lifecycle models should be introduced with their first production consumers.

## Restart checklist

1. Run `git status --short --branch` and inspect the local slice 3A commit; Codex does not push automatically.
2. Read ADRs 0006–0008 and the Catalog Core/type-mapper/introspector implementation and tests before extending the provider.
3. Re-run production restore, build, tests, formatting, Markdown-link validation, and `git diff --check` after any change.
4. Use the deterministic SQL Server fixture for introspection work; do not replace the real provider path with mocks or build-only evidence.
5. Preserve the Core dependency direction and never introduce SQL fragments, provider client types, semantic rules, or protocol concerns into the technical catalog domain.
