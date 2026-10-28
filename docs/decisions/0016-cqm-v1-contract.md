# ADR 0016 — CQM v1 contract and strict structural validation

- **Status:** Proposed
- **Date:** 2026-10-28

## Context

Milestone 1 is complete: the catalog foundation can introspect SQL Server, import and validate semantic overlays, merge them, persist revisions, activate/recover them, deserialize them, and search them. Milestone 2 starts with the handoff's fourth recommended implementation step: "CQM DTOs/schema/normalization/types/validation" (`docs/AI_DATA_GATEWAY_HANDOFF.md` §6, §24, §27). The first backlog item is deliberately narrower: define versioned strict CQM DTOs and a published JSON Schema.

The CQM is the single provider-neutral query contract every protocol adapter must produce. It must be impossible to represent writes, raw SQL, or SQL fragments in it. Protocol syntax (MCP, OData, JSON) never reaches the provider directly.

## Decision

- Add the immutable CQM v1 types under `TheSqlODataMcp.Core.Cqm`: `CqmQuery`, `CqmEntitySource`, `CqmJoin`, `CqmSelectItem`, `CqmOrderBy`, `CqmPage`, and the six expression forms (`CqmFieldExpression`, `CqmLiteralExpression`, `CqmAliasExpression`, `CqmOperatorExpression`, `CqmFunctionExpression`, `CqmAggregateExpression`).
- Version the contract as `1.0` (`CqmVersions.V1`). The root query always requires `version`, `from`, and an explicit bounded `page`; `joins`, `select`, `groupBy`, and `orderBy` default to empty collections. `where` and `having` are nullable.
- Use exactly one expression form per expression object. Field references use `alias.field`; aliases must be safe SQL identifiers (`[A-Za-z_][A-Za-z0-9_]*`, maximum 128 characters). Joins require exactly one of `on` or `relationship`.
- Use the handoff's canonical operator, function, and aggregate names without translation at this layer (`and`, `notIn`, `dateTrunc`, `count`, and so on). The existing `CanonicalScalarType` vocabulary is reused for typed literals; `unknown` is not a valid declared literal type.
- Embed a strict JSON Schema at `src/TheSqlODataMcp.Core/Cqm/Schemas/cqm.v1.schema.json` (`$id` `urn:thesqlodatamcp:cqm:1.0`) and expose it through `CqmSchema.Json` / `CqmJson.SchemaJson`. The schema uses `additionalProperties: false` throughout, `oneOf` for the expression and join-condition unions, required `from`/`page`/`version`, enums for the v1 operator/function/aggregate vocabulary, bounded page integers, and type-compatible typed literals.
- Add `CqmJson.Deserialize` as the strict JSON entry point: reject duplicate property names and unpaired UTF-16 surrogates, parse JSON, evaluate the published schema (sanitizing only out-of-decimal-range numeric tokens for the schema evaluator so valid `double`/`json` literals do not crash it; typed construction still sees the original JSON), reject unknown/invalid structure, deserialize with `JsonUnmappedMemberHandling.Disallow`, then run `CqmValidator` for rules JSON Schema cannot express. Schema diagnostics filter failed conditional branches and successful-ancestor noise so reported paths point at actionable locations. `CqmJson.Serialize` emits the same camel-case contract and round-trips through `Deserialize`.
- Add `CqmValidator` for catalog-independent structural rules: duplicate source/select aliases, alias scope, alias references only in `having`/`orderBy`, field-reference shape, v1 join-condition shape (`eq(field, field)` or an `and` conjunction of equality, referencing the joined alias), operator/function arity, aggregate placement and nesting, aggregate expression requirements, `having` classification, and division by a literal zero. Typed literal constructors additionally enforce canonical GUID/date/binary lexical form, parseable time/datetime/datetimeoffset values, and numeric ranges (including finite `double` and exact-zero checks). Every problem carries a stable `cqm.*` code and a JSON-path-style path.
- **Deliberately out of scope:** catalog entity/field/relationship resolution, type inference, expression normalization, group-by completeness, join-priority resolution, administrator limits, and the SQL Server compiler/executor. Those remain the next Milestone 2 slices. The structural validator is a foundation for them, not a substitute.
- The schema evaluation is serialized with a lock, matching the already-proven `SemanticOverlayImporter` decision: JsonSchema.Net evaluation of a shared recursive schema is not safe to call concurrently without protection, and CQM parsing can happen concurrently in a host.

## Consequences

- Protocol adapters now have one strict target shape. Unknown properties such as `sql` or any write-shaped object are rejected before any catalog or provider code runs.
- `CqmJson.Deserialize` returns collected `CqmValidationError` values instead of throwing per violation, so a caller can see every structural problem in one pass.
- The CQM contract is published as an embedded resource and is ready for Milestone 3's JSON API to serve as a stable schema artifact.
- No query can be executed yet: there is no catalog resolver, type checker, join resolver, normalizer, provider compiler, or executor. Compilation and execution remain later Milestone 2 work.
- The validator deliberately does not reject selected non-aggregate expressions absent from `groupBy`; that depends on expression equivalence and type information in the next slice.

## Acceptance evidence

Local implementation verification:

- `dotnet build thesqlodatamcp.slnx --no-restore`: passed with zero warnings and zero errors.
- `dotnet test thesqlodatamcp.slnx --no-build --no-restore --filter "Category!=SqlServerIntegration"`: 324 passed, 0 failed, 0 skipped (Core 191, SqlServer 94, Persistence 34, Integration 4, Protocol 1); 67 new CQM tests.
- The new tests cover the handoff's entity and analytical examples, schema publication, round-trip idempotence, malformed/duplicate-property/out-of-decimal-range JSON handling, valid huge `double`/`json` literals, double overflow/underflow and subnormal cases, unpaired UTF-16 surrogates (including direct construction), unknown properties, missing required fields, multiple expression forms, invalid joins/enums/page/literal types, alias scope/uniqueness, valid and invalid join-condition shapes, arity, nested aggregates, aggregate placement, `having` classification, division by zero, typed date/GUID lexical checks, clean diagnostic paths, concurrent parsing, and direct construction invariants.

GitHub Actions revalidation is required before this ADR moves to Accepted.
