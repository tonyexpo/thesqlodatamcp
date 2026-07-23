# Development state

**Checkpoint date:** 2026-07-23

**Branch:** `main`

**Milestone:** 1 — Catalog foundation (in progress)

This file is the restart point when conversational context is unavailable. Read it after `AGENTS.md` and the project skill.

## Operating model

- The primary Codex agent is the software architect and QA lead.
- Production implementation is delegated to a `gpt-5.6-terra` sub-agent with bounded scope and acceptance criteria.
- The primary agent owns architecture, review, automated-test adequacy, final validation, ADRs, backlog, changelog, and this checkpoint.
- The canonical project skill is `skills/thesqlodatamcp-technical-lead/SKILL.md`.
- Repository-local `.codex` and `.agents` may be mounted read-only; the version-controlled project skill remains canonical.

## Session checkpoint — 2026-07-23

Milestone 0 is complete. Commit `54a31dd` is present on both local `main` and `origin/main`. GitHub Actions run [29778536859](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/29778536859) passed on the intended Ubuntu runner:

- `validate` passed restore, warning-free build, all production tests, formatting, and offline Markdown-link validation;
- the dependent `sqlserver-integration` job passed Docker discovery, restore, warning-free spike build, static fixture tests, and the real owned-Testcontainers SQL Server test;
- the real test bootstrapped and validated the deterministic database, executed an explicitly typed parameterized query, dropped the fixed database, and proved it absent.

ADR 0004 is therefore Accepted, and the Milestone 0 CI and disposable SQL Server backlog items are closed. Local agent sandboxes can still deny `/var/run/docker.sock`; this no longer blocks the accepted CI infrastructure.

The first three bounded Milestone 1 slices are present on `origin/main`: `cd29eeb` establishes the technical Catalog Core, `3d0cc50` adds SQL Server catalog type mapping, and `c3ea644` plus corrective follow-up `deb5b33` establish the accepted SQL Server table/view/column introspection foundation.

Production implementation for every slice was delegated to `gpt-5.6-terra`; the primary agent retained architecture and acceptance ownership, reviewed the complete diff, added independent QA, and ran the available validation.

The next relational-metadata slice is implemented on local `main` and recorded by proposed ADR 0009. It extends the production introspector with primary/alternate keys, useful standalone rowstore indexes, and ordered foreign-key relationships through one fixed read-only command with three result sets. Local validation is complete; the slice is not accepted and its backlog item remains open until the dedicated GitHub Actions job passes against the real SQL Server fixture.

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

This accepted slice contains no catalog SQL, connection handling, discovery, or entity construction. The separately accepted slice below begins that provider work.

### Milestone 1 slice 3A — SQL Server table/view/column introspection

ADR 0008 records the accepted introspection foundation:

- exposes a connection-string/timeout/cancellation contract without public SQL client types;
- executes one fixed read-only `SELECT` over `sys.objects`, schemas, columns, types, tables, computed columns, and extended properties;
- discovers non-shipped user tables and views while excluding system schemas, temporal history tables, and non-`U`/`V` programmable or auxiliary objects by construction;
- constructs deterministic entities and fields with ordinal casing/order, ADR 0007 type mapping, descriptions, nullability, identity, computed/persisted-computed, temporal-period, and rowversion metadata;
- deliberately leaves keys, indexes, and relationships empty for the next slice;
- includes a production Testcontainers integration test and a dedicated CI route against the fixed SQL Server fixture.

GitHub Actions run [29951320005](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/29951320005) passed the complete `validate` job and reached the real production Testcontainers test. That test exposed a provider-boundary defect: `sys.objects.type` is fixed-width `char(2)`, while the strict projection accepts canonical `U`/`V` values. Commit `deb5b33` normalizes that catalog value to `varchar(1)` in the fixed query and adds a static regression assertion.

GitHub Actions run [29953151060](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/29953151060) then passed both `validate` and the dependent `sqlserver-integration` job on commit `deb5b33`. The production introspector discovered the expected twelve user tables and two views, excluded the temporal history table and unsupported objects, projected representative metadata, produced identical canonical JSON and hashes across repeated discovery, and tore down the fixed database. ADR 0008 is therefore Accepted. The local environment's denial of `/var/run/docker.sock` no longer blocks this slice because the intended runner supplied the required real-provider evidence.

## Implemented, pending real-provider acceptance

### Milestone 1 slice 3B — SQL Server relational metadata introspection

Proposed ADR 0009 records the locally validated design:

- one fixed read-only command now returns separate ordered result sets for columns, keys/indexes, and foreign keys without N+1 access or caller-controlled SQL;
- `PK` and `UQ` constraints become primary/alternate `CatalogKey` values with composite order preserved;
- enabled, non-hypothetical rowstore indexes become `CatalogIndex` values while heaps, included columns, non-rowstore indexes, and PK/UQ backing indexes are excluded;
- simple, multiple-to-one, composite, and self foreign keys become named `CatalogRelationship` values with ordered source/target field pairs;
- projection rejects unsupported metadata kinds, inconsistent grouped flags or targets, ordinal gaps, orphan sources, missing targets, target-identity mismatches, and missing target fields;
- canonical JSON and structural hashes remain independent of input row order and database collation;
- the real fixture now includes a standalone composite index with an included column, and production integration assertions cover filtered/standalone indexes, constraint-index exclusion, primary/alternate/composite keys, and both ambiguous composite address relationships.

The primary review caught four defects or evidence gaps before acceptance: `sys.index_columns.key_ordinal` required an explicit `int` conversion for `SqlDataReader.GetInt32`; orphan source metadata was silently ignored; ordinal gaps were accepted; and the integration assertions did not fully prove backing-index exclusion and both composite address relationships. The delegated implementer corrected all four, and the primary agent added an independent row-order determinism regression test.

ADR 0009 remains Proposed because this environment cannot access Docker. After the local commits are pushed, require a green `validate` job and dependent `sqlserver-integration` job before changing the ADR to Accepted or closing the complete SQL Server introspection backlog item.

## QA evidence at this checkpoint

### Remote CI evidence

- GitHub Actions run `29778536859`: success.
- `validate`: success.
- `sqlserver-integration`: success through the owned pinned Testcontainers path.
- GitHub Actions run `29951320005`: `validate` passed; the production SQL Server integration job passed Docker, fixture, restore, and build steps but failed its final introspector test because the fixed-width object type reached strict projection with padding.
- GitHub Actions run `29953151060`: success; both `validate` and `sqlserver-integration` passed, including the corrected production introspector against the real disposable SQL Server fixture.

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

### Local introspection-foundation evidence

- `dotnet restore thesqlodatamcp.slnx`: passed; all projects up to date.
- `dotnet build thesqlodatamcp.slnx --no-restore`: passed with zero warnings and zero errors.
- `dotnet test tests/TheSqlODataMcp.SqlServer.Tests/TheSqlODataMcp.SqlServer.Tests.csproj --no-build --no-restore`: 87 passed, 0 failed, 0 skipped.
- `dotnet test tests/TheSqlODataMcp.IntegrationTests/TheSqlODataMcp.IntegrationTests.csproj --no-build --no-restore --filter "Category!=SqlServerIntegration"`: 4 passed, 0 failed, 0 skipped.
- `dotnet test thesqlodatamcp.slnx --no-build --no-restore --filter "Category!=SqlServerIntegration"`: 104 passed, 0 failed, 0 skipped across all four production test projects.
- Independent QA verifies the single fixed read-only statement, structural object filters, declared-user-type `unknown` behavior, deterministic projection, and the real-fixture expectations.
- `docker info`: failed because access to `/var/run/docker.sock` is denied in this agent environment; the same result occurred with authorized execution.
- The `Category=SqlServerIntegration` production test passed on the intended runner in GitHub Actions run `29953151060`.

The ordinary sandbox denied VSTest sockets and Roslyn/MSBuild pipes. Those commands were independently rerun with authorized execution and passed. This remains an environment constraint rather than a product defect.

### Local relational-metadata evidence

- `dotnet restore thesqlodatamcp.slnx`: passed; all projects up to date.
- `dotnet build thesqlodatamcp.slnx --no-restore`: passed with zero warnings and zero errors.
- `dotnet test tests/TheSqlODataMcp.SqlServer.Tests/TheSqlODataMcp.SqlServer.Tests.csproj --no-restore`: 94 passed, 0 failed, 0 skipped after the independent determinism regression was added.
- `dotnet test thesqlodatamcp.slnx --no-build --no-restore --filter "Category!=SqlServerIntegration"`: 111 passed, 0 failed, 0 skipped across all four production test projects.
- `dotnet format thesqlodatamcp.slnx --verify-no-changes --no-restore`: passed.
- `bash eng/verify-markdown-links.sh`: passed.
- `git diff --check`: passed.
- Independent QA covers the fixed three-result-set/read-only command, provider integer conversion, relational grouping and ordering, orphan/target rejection, constraint-backing index exclusion, exact composite relationship pairs, and canonical row-order independence.
- Real `Category=SqlServerIntegration` execution remains pending on the intended Docker-capable GitHub Actions runner.

## Open work and risks

### SQL Server catalog introspection

The table/view/column introspection foundation is accepted. Relational metadata is implemented and locally validated, but real-provider acceptance is pending.

Do not mark ADR 0009 Accepted or close the full introspection backlog item until GitHub Actions proves the production code against the disposable SQL Server fixture without weakening the existing table/view/column, metadata, keyless-view, type, exclusion, determinism, and teardown coverage.

### Catalog lifecycle remains pending

Semantic Markdown/YAML merge, strict structural validation, capability models, SQLite revision persistence, atomic activation/rollback, bootstrap modes, and in-memory search are not implemented. Do not mark the remaining Milestone 1 backlog items complete.

### Dynamic Client Registration

OpenIddict 7.6.0 does not implement RFC 7591 Dynamic Client Registration. Before Milestone 5, design and security-test a bounded registration endpoint backed by OpenIddict's application manager, or validate a dedicated component. Do not weaken redirect-URI, client-type, registration-rate, or resource validation.

## Next dependency-ordered work

1. Push the local relational-metadata implementation and documentation commits.
2. Require green `validate` and `sqlserver-integration` jobs, then record the run in ADR 0009 and this checkpoint, mark the ADR Accepted, and close the complete introspection backlog item.
3. Proceed to semantic Markdown/YAML merge and strict validation after complete introspection; capability and revision lifecycle models should be introduced with their first production consumers.

## Restart checklist

1. Run `git status --short --branch`; Codex does not push automatically.
2. Read ADRs 0006–0009 and the Catalog Core/type-mapper/introspector implementation and tests before extending the provider.
3. Re-run production restore, build, tests, formatting, Markdown-link validation, and `git diff --check` after any change.
4. Use the deterministic SQL Server fixture for introspection work; do not replace the real provider path with mocks or build-only evidence.
5. Preserve the Core dependency direction and never introduce SQL fragments, provider client types, semantic rules, or protocol concerns into the technical catalog domain.
