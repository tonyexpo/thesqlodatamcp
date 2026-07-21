# ADR 0007 — SQL Server catalog type mapping

- **Status:** Accepted
- **Date:** 2026-07-21

## Context

ADR 0006 defines the provider-neutral scalar vocabulary and requires inert provider details, but it deliberately leaves provider introspection outside Core. The SQL Server introspector needs a deterministic boundary that translates catalog metadata into that vocabulary without exposing `Microsoft.Data.SqlClient` types, accepting structurally impossible metadata, or silently approximating unsupported types.

SQL Server reports column type metadata as a provider name plus `max_length`, precision, and scale. Length is expressed in bytes for Unicode character types, `-1` is the `max` sentinel for selected variable-length types, and decimal, float, and scale-bearing temporal types have discrete storage bands.

## Decision

- Keep SQL Server type mapping in `TheSqlODataMcp.SqlServer`; Core remains provider-neutral.
- Accept a small immutable metadata input containing provider type name, maximum length, precision, and scale. Do not expose SQL client objects or catalog-reader implementation details through the public mapping API.
- Normalize provider type names with invariant lowercase and collapsed whitespace before matching and persistence.
- Map the supported SQL Server scalar families into the ADR 0006 canonical vocabulary:
  - integral and Boolean types;
  - decimal, money, real, and float types;
  - character and binary types, including `max` variants and deprecated LOB names;
  - GUID, date, time, date-time, date-time-offset, and rowversion types.
- Preserve meaningful provider length, precision, scale, and deterministic store representation. Convert Unicode `max_length` bytes to character counts.
- Validate exact fixed metadata and the documented decimal, float, and temporal storage bands. Reject impossible known-type combinations instead of coercing or guessing.
- Map `xml`, `sql_variant`, spatial/hierarchical types, user-defined names, and all other unrecognized provider names to `unknown`. Preserve the normalized name but do not invent Core length, precision, or scale metadata.
- Keep catalog queries, connections, schema discovery, and construction of catalog entities outside this mapper.

## Consequences

- The future introspector has one deterministic and independently testable type boundary.
- Unsupported provider types remain visible in the technical catalog without being misrepresented as a supported scalar type.
- A malformed or incorrectly projected catalog row fails explicitly, making introspector defects observable.
- Alias/user-defined types remain conservative `unknown` values until a future bounded decision defines safe base-type resolution and provider-detail preservation.
- This decision does not demonstrate SQL Server introspection; that remains a separate Milestone 1 gate requiring the real disposable fixture.
