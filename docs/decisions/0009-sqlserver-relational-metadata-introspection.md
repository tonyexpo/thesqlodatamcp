# ADR 0009 — SQL Server relational metadata introspection

- **Status:** Proposed
- **Date:** 2026-07-23

## Context

ADR 0008 establishes the accepted table, view, and column discovery boundary while deliberately leaving keys, useful indexes, and foreign-key relationships empty. The next bounded slice must complete physical relational metadata without introducing caller-controlled SQL, N+1 catalog access, constraint-backing index duplication, database-collation-dependent ordering, or incomplete relationships to excluded objects.

SQL Server exposes this metadata through several related `sys.*` views. Joining every catalog surface into one rectangular result would multiply rows across columns, indexes, and foreign keys. Issuing one command per object would instead create an avoidable N+1 path.

## Decision

- Retain the ADR 0008 public connection-string, timeout, cancellation, and provider-neutral output contract.
- Replace its single-result-set query with one fixed, read-only command containing exactly three ordered `SELECT` result sets: columns, keys/indexes, and foreign keys. Callers still supply no SQL, identifiers, filters, or projections.
- Project `PK` and `UQ` constraints as `CatalogKey` values. Preserve physical constraint names, distinguish the single primary key, and retain composite field order from `sys.index_columns.key_ordinal`.
- Project enabled, non-hypothetical rowstore indexes as `CatalogIndex` values. Exclude heaps, non-rowstore indexes, included columns, and the backing indexes of primary/unique constraints. Preserve uniqueness and filtered-index flags.
- Project foreign keys as named `CatalogRelationship` values. Preserve source-to-target field-pair order from `constraint_column_id`, multiple relationships to the same target, composite relationships, and self-references.
- Apply the same structural exclusions as column discovery to relational metadata. A foreign-key target must be present in the discovered technical catalog, its physical identity must match, and every target field must exist.
- Treat provider metadata as untrusted at the projection boundary: require positive identifiers, supported metadata kinds, internally consistent grouped flags/targets, contiguous ordinals starting at one, known source objects, and known target objects/fields.
- Sort unordered entities and named metadata with ordinal in-memory comparison. Preserve the meaningful physical order of key fields, index key fields, and relationship pairs so canonical JSON and structural hashes do not depend on catalog row order or database collation.
- Keep the production Testcontainers integration test as the acceptance gate. It must prove primary and alternate keys, composite keys, a filtered unique index, a standalone composite index without included columns, exclusion of constraint-backing indexes, simple/composite/ambiguous/self foreign keys, deterministic repeat discovery, existing structural exclusions, and fixture teardown.

## Acceptance evidence

Local validation on 2026-07-23 passed restore, a warning-free solution build, 111 non-Docker tests, formatting, offline Markdown-link validation, and `git diff --check`. Independent QA additionally found and corrected a SQL reader type mismatch for `sys.index_columns.key_ordinal`, silent acceptance of orphan source metadata, non-contiguous metadata ordinals, and incomplete integration assertions.

The local agent environment cannot access `/var/run/docker.sock`. This ADR therefore remains Proposed until the implementation passes the dedicated `sqlserver-integration` job against the real deterministic SQL Server fixture on the intended GitHub Actions runner.

## Consequences

- Complete physical relational metadata is obtained with one database round trip and no caller-controlled SQL.
- Primary/unique constraint semantics remain distinct from standalone index hints, preventing duplicate catalog entries for their backing indexes.
- Relationship construction fails closed when structural filtering or malformed provider rows would otherwise leave dangling metadata.
- Included index columns, non-rowstore index details, check/default constraints, referential actions, trust/disabled flags, and index filter predicates are outside this slice. Future support requires a bounded decision and compatible Core consumer.
- Semantic overlays, configured relationships, logical keys for keyless views, capability models, and revision lifecycle remain later Milestone 1 work.
