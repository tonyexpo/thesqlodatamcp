# ADR 0006 — Provider-neutral technical catalog core

- **Status:** Accepted
- **Date:** 2026-07-21

## Context

Milestone 1 needs one stable technical-catalog representation before the SQL Server introspector, semantic overlay, control-store revisions, search, and protocol adapters are implemented. The representation must preserve provider metadata without introducing a dependency from Core to SQL Server or allowing provider SQL to enter the shared domain.

Catalog snapshots also need a deterministic structural identity so later refresh and revision logic can distinguish real schema changes from differences in discovery enumeration order.

## Decision

- Define the technical catalog in `TheSqlODataMcp.Core.Catalog` with no provider or product-project dependency.
- Represent physical object identity as separate schema and object names. Preserve identifier casing and compare identifiers with ordinal semantics.
- Use the exact v1 canonical scalar vocabulary from the architecture handoff while retaining provider type name, store representation, length, precision, and scale as inert metadata.
- Represent tables and keyless or keyed views, fields, primary/alternate keys, useful indexes including the filtered flag, and relationships with ordered field pairs.
- Preserve identity, computed, persisted-computed, temporal-period, rowversion, and entity temporal metadata with construction-time structural invariants.
- Defensively copy all input collections and reject duplicate or missing local references. Key/index field order and relationship-pair order remain semantically significant.
- Produce canonical camel-case JSON with stable ordinal sorting for entities and named metadata. Compute a lowercase SHA-256 structural hash over that representation; timestamps and environment-dependent values are excluded.
- Keep revision persistence/status, semantic overlays, catalog search, CQM, and provider introspection outside this initial slice.

## Consequences

- SQL Server introspection can target a provider-neutral model without leaking `Microsoft.Data.SqlClient` types or SQL text into Core.
- Keyless views remain representable and no synthetic key is introduced.
- Logically identical snapshots have the same canonical JSON and hash even when discovery returns entities or named metadata in a different order.
- Changes to ordered key/index columns or relationship pairs remain observable structural changes.
- Future changes to the canonical representation require compatibility review because they can change persisted snapshot hashes.
- Capability and revision models remain open Milestone 1 work and must not be inferred as completed by this ADR.
