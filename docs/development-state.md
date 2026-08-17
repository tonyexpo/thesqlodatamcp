# Development state

**Checkpoint date:** 2026-08-17

**Branch:** `main`

**Milestone:** 1 — Catalog foundation (in progress)

This file is the restart point when conversational context is unavailable. Read it after `AGENTS.md` and the project skill.

## Operating model

- The primary agent (Codex or Claude Code) is the software architect and QA lead.
- Production implementation is delegated to a dev-senior sub-agent with bounded scope and acceptance criteria: `gpt-5.6-terra` when the primary agent is Codex, `Sonnet-5` when the primary agent is Claude Code.
- The primary agent owns architecture, review, automated-test adequacy, final validation, ADRs, backlog, changelog, and this checkpoint.
- The canonical project skill is `skills/thesqlodatamcp-technical-lead/SKILL.md`.
- Repository-local `.codex` and `.agents` may be mounted read-only; the version-controlled project skill remains canonical.

## Session checkpoint — 2026-08-17

Milestone 0 is complete. Commit `54a31dd` is present on both local `main` and `origin/main`. GitHub Actions run [29778536859](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/29778536859) passed on the intended Ubuntu runner:

- `validate` passed restore, warning-free build, all production tests, formatting, and offline Markdown-link validation;
- the dependent `sqlserver-integration` job passed Docker discovery, restore, warning-free spike build, static fixture tests, and the real owned-Testcontainers SQL Server test;
- the real test bootstrapped and validated the deterministic database, executed an explicitly typed parameterized query, dropped the fixed database, and proved it absent.

ADR 0004 is therefore Accepted, and the Milestone 0 CI and disposable SQL Server backlog items are closed. Local agent sandboxes can still deny `/var/run/docker.sock`; this no longer blocks the accepted CI infrastructure.

All four bounded Milestone 1 introspection slices are present on `origin/main`: `cd29eeb` establishes the technical Catalog Core, `3d0cc50` adds SQL Server catalog type mapping, `c3ea644` plus corrective follow-up `deb5b33` establish the accepted SQL Server table/view/column introspection foundation, and `f686dff` extends the production introspector with primary/alternate keys, useful standalone rowstore indexes, and ordered foreign-key relationships through one fixed read-only command with three result sets.

Production implementation for every slice was delegated to `gpt-5.6-terra`; the primary agent retained architecture and acceptance ownership, reviewed the complete diff, added independent QA, and ran the available validation.

This checkpoint corrects a stale restart record: the 2026-07-23 checkpoint recorded the relational-metadata slice as locally validated but pending push and CI. Verification on 2026-08-17 found that `f686dff` was already on `origin/main` and that GitHub Actions run [30031601860](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/30031601860) had already passed both the `validate` and `sqlserver-integration` jobs against the real deterministic SQL Server fixture. ADR 0009 is accordingly now Accepted, and the complete SQL Server introspection backlog item (tables, views, columns, keys, indexes, foreign keys) is closed. No further push or CI run is outstanding for this slice.

The same session then delegated Milestone 1 slice 4A — semantic overlay Markdown/YAML import and strict validation — to a Sonnet-5 dev-senior sub-agent, bounded to `TheSqlODataMcp.Core` only and explicitly excluding catalog merge. The primary agent independently reviewed the complete diff line by line (including re-deriving why every null-forgiving operator in the importer is safe given the JSON Schema's `required` fields), independently re-ran every claimed verification command rather than accepting the sub-agent's report, byte-level-verified the sub-agent's claim that `dotnet format` ENDOFLINE failures are a pre-existing local `core.autocrlf` artifact unrelated to the new files, and added ten further independent QA tests closing gaps the delegated tests left (an unreached error path, ordinal case-sensitivity of physical-reference resolution, an unknown key directly on an entity object, and the domain model's own construction-time invariants). ADR 0010 records this design. It is Proposed pending a green `validate` CI run; unlike the SQL Server slices, it needs no real database and so does not depend on the `sqlserver-integration` job.

The push (commit `05f96e1`) was made and its CI run ([32058513059](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/32058513059)) failed — not on anything in the slice itself, but at the very first `validate` step, `dotnet restore thesqlodatamcp.slnx`, on a `NU1903` advisory unrelated to this diff (see "Open work and risks" for the full root-cause and fix). That fix is prepared and locally verified in this same session; see "Next dependency-ordered work" for the remaining push-and-confirm step before ADR 0010 can be marked Accepted.

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

### Milestone 1 slice 3B — SQL Server relational metadata introspection

ADR 0009 records the accepted design:

- one fixed read-only command now returns separate ordered result sets for columns, keys/indexes, and foreign keys without N+1 access or caller-controlled SQL;
- `PK` and `UQ` constraints become primary/alternate `CatalogKey` values with composite order preserved;
- enabled, non-hypothetical rowstore indexes become `CatalogIndex` values while heaps, included columns, non-rowstore indexes, and PK/UQ backing indexes are excluded;
- simple, multiple-to-one, composite, and self foreign keys become named `CatalogRelationship` values with ordered source/target field pairs;
- projection rejects unsupported metadata kinds, inconsistent grouped flags or targets, ordinal gaps, orphan sources, missing targets, target-identity mismatches, and missing target fields;
- canonical JSON and structural hashes remain independent of input row order and database collation;
- the real fixture now includes a standalone composite index with an included column, and production integration assertions cover filtered/standalone indexes, constraint-index exclusion, primary/alternate/composite keys, and both ambiguous composite address relationships.

The primary review caught four defects or evidence gaps before acceptance: `sys.index_columns.key_ordinal` required an explicit `int` conversion for `SqlDataReader.GetInt32`; orphan source metadata was silently ignored; ordinal gaps were accepted; and the integration assertions did not fully prove backing-index exclusion and both composite address relationships. The delegated implementer corrected all four, and the primary agent added an independent row-order determinism regression test.

GitHub Actions run [30031601860](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/30031601860) passed both the `validate` and `sqlserver-integration` jobs on commit `f686dff`, proving the production key/index/foreign-key projection against the real deterministic SQL Server fixture. ADR 0009 is therefore Accepted, and the complete SQL Server introspection backlog item is closed.

## Implemented, pending CI acceptance

### Milestone 1 slice 4A — semantic overlay import and validation

ADR 0010 records the proposed design:

- `SemanticOverlay` and related types in `TheSqlODataMcp.Core`, following `TechnicalCatalog`'s construction-time invariant discipline (defensive copies, ordinal comparison, rejection of null/duplicate/empty-required input);
- `SemanticOverlayImporter` with two entry points (combined Markdown-with-front-matter, and separate YAML plus Markdown), both validated against a supplied `TechnicalCatalog`;
- two required, independent strictness stages on every import: strict typed YAML deserialization (unknown-key rejection, including the six forbidden top-level sections) and versioned JSON Schema evaluation (`additionalProperties: false` throughout, catching structural/cross-field rules typed deserialization alone cannot);
- physical-reference validation: entity `source` and relationship `target` must resolve to a discovered entity, `fields`/join keys must resolve to existing fields, duplicate entity sources are rejected;
- a collected-errors result type (`SemanticOverlayImportResult`/`SemanticOverlayValidationError`, stable code + path + message) instead of throw-per-violation, so an administrator sees every problem in one pass;
- the Markdown narrative is opaque administrator-authored text, never parsed into rules.

This slice does not merge the overlay into a `TechnicalCatalog`, reconcile `catalogVersion` against the active catalog, or implement FK/YAML relationship merging, YAML-wins precedence, or keyless-view logical keys — all deferred to the next slice.

This slice requires no real SQL Server access. ADR 0010 remains Proposed until a green `validate` job is confirmed on `origin/main` for the commit that includes it.

## QA evidence at this checkpoint

### Remote CI evidence

- GitHub Actions run `29778536859`: success.
- `validate`: success.
- `sqlserver-integration`: success through the owned pinned Testcontainers path.
- GitHub Actions run `29951320005`: `validate` passed; the production SQL Server integration job passed Docker, fixture, restore, and build steps but failed its final introspector test because the fixed-width object type reached strict projection with padding.
- GitHub Actions run `29953151060`: success; both `validate` and `sqlserver-integration` passed, including the corrected production introspector against the real disposable SQL Server fixture.
- GitHub Actions run [30031601860](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/30031601860): success; both `validate` and `sqlserver-integration` passed on commit `f686dff`, proving the production key/index/foreign-key projection against the real disposable SQL Server fixture and closing the introspection backlog item.

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
- Real `Category=SqlServerIntegration` execution passed on the intended Docker-capable GitHub Actions runner in run `30031601860`.

### Local semantic-overlay evidence

- `dotnet restore thesqlodatamcp.slnx`: 8 of 9 projects restore cleanly; `TheSqlODataMcp.IntegrationTests` fails on a pre-existing, unrelated `NU1903` advisory (see "Open work and risks" below) — confirmed via `git diff --stat` that neither that project nor `Directory.Packages.props` is touched by this slice.
- `dotnet build thesqlodatamcp.slnx --no-restore`: the 8 restorable projects build with zero warnings/errors.
- `dotnet test tests/TheSqlODataMcp.Core.Tests/TheSqlODataMcp.Core.Tests.csproj --no-restore`: 44 passed, 0 failed, 0 skipped (34 delegated + 10 independent QA).
- `dotnet test thesqlodatamcp.slnx --no-build --no-restore --filter "Category!=SqlServerIntegration"`: Core.Tests 44, SqlServer.Tests 94, ProtocolTests 1, all passed; `IntegrationTests.dll` not found (never built, same pre-existing blocker, not a test failure).
- `dotnet format thesqlodatamcp.slnx --verify-no-changes --no-restore`: fails only on pre-existing tracked files unrelated to this diff. Byte-level inspection (`od -An -c`) confirmed all six new files are pure LF on disk while flagged pre-existing files are CRLF; this machine's `git config core.autocrlf` is `true` with no repository `.gitattributes` override, so long-checked-out files materialize CRLF locally while newly written files do not. This is a local-checkout artifact, not a product or diff defect, and does not affect the Linux CI runner.
- Independent QA (`SemanticOverlayImporterQaTests.cs`) covers the previously unreached `overlay.frontMatterMissing` path, ordinal case-sensitivity of entity-source and field-key resolution, an unknown key directly on an entity object, a minimal zero-entity overlay, and direct construction-time invariants of the domain types (join field pairs, non-empty relationship joins, warnings, duplicate entity sources, duplicate field/relationship map keys) independent of the YAML pipeline.
- This slice requires no real SQL Server access; there is no `sqlserver-integration` dependency to satisfy.

## Open work and risks

### SQL Server catalog introspection

The complete table/view/column/key/index/foreign-key introspection is accepted, with real-provider evidence in GitHub Actions run `30031601860`. This backlog item is closed; no further acceptance work remains for it.

### Semantic overlay import and validation

Slice 4A (ADR 0010, Markdown/YAML overlay import and strict validation) is implemented and locally validated. Do not mark its backlog items complete or the ADR Accepted until GitHub Actions proves a green `validate` job on `origin/main` for the commit that includes it.

### Catalog lifecycle remains pending

Overlay merge into the technical catalog (FK/YAML relationship combination, YAML-wins precedence, keyless-view logical keys, merged structural hashes), capability models, SQLite revision persistence, atomic activation/rollback, bootstrap modes, and in-memory search are not implemented. Do not mark the remaining Milestone 1 backlog items complete.

### `IntegrationTests` NU1903 restore failure — fixed, CI confirmation pending

The `validate` job for the slice 4A push (commit `05f96e1`, run `32058513059`) failed at its very first step, `dotnet restore thesqlodatamcp.slnx`: `NU1903` on `SSH.NET` 2024.2.0, pulled in transitively via `Testcontainers.MsSql` 4.8.1's `Testcontainers` base dependency ([GHSA-q939-rpr3-3284](https://github.com/advisories/GHSA-q939-rpr3-3284)). Because Central Package Management restores the whole graph, this blocked `validate` for the entire solution regardless of which project a change touched — not only local checkouts, as first assessed.

Fixed by bumping `Testcontainers.MsSql` to 4.14.0 (both centrally and in the independently pinned `spikes/platform/sqlserver-tests/SqlServerTests.ApiSpike.csproj`), which pulls a `Testcontainers` version requiring the patched `SSH.NET >= 2026.0.0`. This surfaced a second, unrelated break: `MsSqlBuilder`'s parameterless constructor is now obsolete (an error under this repo's warnings-as-errors policy), fixed by passing the already-pinned image directly to `new MsSqlBuilder(SqlServerImage)` in `SqlServerReportingCatalogFixture.cs` and the spike's `MsSqlContainerTests.cs`. See ADR 0004's subsequent-evidence note.

Local verification: full-solution `dotnet restore`/`build` now succeed for all 9 projects with zero warnings; `dotnet test thesqlodatamcp.slnx --filter "Category!=SqlServerIntegration"` passes (Core.Tests 44, SqlServer.Tests 94, ProtocolTests 1, IntegrationTests 4); the spike restores, builds, and its `Category=FixtureStatic` tests pass. The real Docker-backed `sqlserver-integration` job — the only way to prove the Testcontainers 4.14.0 upgrade still works against a live container, per ADR 0004's own stated policy — has not yet run; this environment has no Docker access. Do not consider this upgrade verified until that job is green.

### Dynamic Client Registration

OpenIddict 7.6.0 does not implement RFC 7591 Dynamic Client Registration. Before Milestone 5, design and security-test a bounded registration endpoint backed by OpenIddict's application manager, or validate a dedicated component. Do not weaken redirect-URI, client-type, registration-rate, or resource validation.

## Next dependency-ordered work

1. Push the local `Testcontainers.MsSql` 4.14.0 / `NU1903` fix commit (see "Open work and risks") and require a green `validate` job, then require the dependent `sqlserver-integration` job to confirm the upgrade against real Docker.
2. Once both jobs are green for the commit that includes it, record the run in ADR 0010 and this checkpoint and mark the ADR Accepted.
3. Implement the overlay merge slice (4B): merge precedence into `TechnicalCatalog`, FK/YAML relationship combination, YAML-wins override semantics, keyless-view logical keys, and merged structural hashing.
4. Introduce capability and revision/lifecycle models with their first production consumers rather than speculatively.
5. Add SQLite control-store migrations and catalog revision persistence once the merge and validation boundary is settled.

## Restart checklist

1. Run `git status --short --branch`; the primary agent does not push automatically.
2. Read ADRs 0006–0010 and the Catalog Core/type-mapper/introspector/semantic-overlay implementation and tests before extending the catalog domain.
3. Re-run production restore, build, tests, formatting, Markdown-link validation, and `git diff --check` after any change. `dotnet restore`/`build` on the full solution currently fails only on the pre-existing `IntegrationTests` `NU1903` advisory; scope other projects explicitly (e.g. `dotnet test tests/TheSqlODataMcp.Core.Tests/...`) until that is resolved.
4. Use the deterministic SQL Server fixture for introspection work; do not replace the real provider path with mocks or build-only evidence.
5. Preserve the Core dependency direction and never introduce SQL fragments, provider client types, protocol concerns, or (for the technical catalog specifically, as opposed to the semantic overlay) semantic rules into the technical catalog domain.
