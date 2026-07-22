# ADR 0008 — SQL Server introspection foundation

- **Status:** Accepted
- **Date:** 2026-07-22

## Context

ADRs 0006 and 0007 define the provider-neutral technical catalog and SQL Server type mapping. The next provider slice must read physical SQL Server metadata without introducing raw-SQL input, provider types in Core, N+1 catalog queries, synthetic keys, or accidental exposure of system and programmable objects.

The complete introspector will eventually discover columns, keys, useful indexes, and foreign-key relationships. A smaller first vertical slice can establish and integration-test the connection, metadata-query, filtering, column projection, and deterministic entity-construction boundary before adding the more relational metadata.

## Decision

- Add a sealed SQL Server introspector whose public inputs are a connection string, positive command timeout, and cancellation token, and whose output is a provider-neutral `TechnicalCatalog`.
- Execute exactly one fixed, read-only `SELECT` over SQL Server `sys.*` catalog views. Do not accept SQL, identifiers, filters, or projections from callers and do not expose `Microsoft.Data.SqlClient` types in the public contract.
- Discover only non-shipped user tables and views outside `sys` and `INFORMATION_SCHEMA`. Exclude temporal history tables and exclude procedures, functions, triggers, sequences, synonyms, table types, and other object kinds by selecting only `U` and `V` objects.
- Normalize the fixed-width `sys.objects.type` value to one variable-width character in the catalog query before strict projection validation, so provider padding cannot alter the provider-neutral object kind.
- Preserve physical identifier casing and `sys.columns.column_id`; sort entities with ordinal schema/object comparison and fields by ordinal in memory so database collation does not define canonical ordering.
- Project nullability, identity, computed/persisted-computed, temporal period, rowversion, object/column `MS_Description`, and the ADR 0007 provider/canonical type mapping.
- Preserve declared user-type names through the `user_type_id` join. Unknown types remain explicit `unknown` values without inferred Core metadata.
- Return empty key, index, and relationship collections in this first slice. Do not synthesize incomplete relational metadata.
- Keep the disposable SQL Server test in the production integration-test project. Ordinary validation excludes only the Docker category; the dedicated SQL Server CI job must execute it against the pinned fixture.

## Acceptance evidence

GitHub Actions run [29953151060](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/29953151060) passed on commit `deb5b33` after an earlier real-provider run exposed and led to correction of fixed-width `sys.objects.type` padding. The intended Docker-capable runner proved that the production introspector:

1. discovers the deterministic fixture's twelve user tables and two views;
2. excludes the temporal history table and all deliberately unsupported objects;
3. projects representative type, description, computed, temporal, identity, and rowversion metadata correctly;
4. produces identical canonical JSON and structural hashes across repeated discovery;
5. tears down the fixed fixture database successfully.

## Consequences

- Unit and static QA can validate query immutability, projection invariants, and dependency direction without a database.
- Real SQL Server execution remains a required regression gate for future introspection changes; the accepted run exercised the production Testcontainers path and deterministic fixture teardown.
- Keys, indexes, foreign keys, and relationship construction remain the next bounded provider slice after this accepted foundation.
