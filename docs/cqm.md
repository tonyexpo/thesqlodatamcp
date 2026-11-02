# Canonical Query Model v1

The Canonical Query Model (CQM) is the one versioned, provider-neutral read-query contract that every protocol adapter produces. MCP, OData, JSON, and HTTP `QUERY` syntax never reaches the SQL Server provider directly.

The normative contract is the strict JSON Schema at [`src/TheSqlODataMcp.Core/Cqm/Schemas/cqm.v1.schema.json`](../src/TheSqlODataMcp.Core/Cqm/Schemas/cqm.v1.schema.json), also exposed at runtime by `CqmSchema.Json` / `CqmJson.SchemaJson`. ADR [0016](./decisions/0016-cqm-v1-contract.md) records the design and its boundaries.

## Shape

```json
{
  "version": "1.0",
  "from": { "entity": "sales.Invoices", "alias": "i" },
  "joins": [],
  "select": [
    { "expression": { "field": "i.Id" }, "alias": "invoiceId" }
  ],
  "where": null,
  "groupBy": [],
  "having": null,
  "orderBy": [
    { "expression": { "field": "i.NetAmount" }, "direction": "desc" }
  ],
  "distinct": false,
  "page": { "limit": 100, "offset": 0 },
  "includeTotalCount": false
}
```

`version`, `from`, and `page` are always required. `page` bounds returned pagination, not database work: source scans,
joins, aggregates and total counts still require the later operational limits/resolver/compiler stages.

## Expressions

Each expression is exactly one of:

- a field reference: `{ "field": "i.NetAmount" }`;
- a literal or typed literal: `{ "value": 1000 }`, `{ "value": "2026-01-01", "type": "date" }`;
- an alias reference, only in `having` and `orderBy`: `{ "alias": "totalAmount" }`;
- an operator application: `{ "operator": "multiply", "arguments": [...] }`;
- a function application: `{ "function": "dateTrunc", "arguments": [...] }`;
- an aggregate: `{ "aggregate": "sum", "expression": {...}, "distinct": false }`.

The v1 operator, function, aggregate, and canonical scalar vocabularies are exactly the handoff's lists. Canonical names stay provider-neutral; `unknown` is not a valid declared literal type.

## Validation boundary

`CqmJson.Deserialize(json)` runs, in order:

1. strict JSON parsing, including rejection of duplicate property names and unpaired UTF-16 surrogates;
2. the published JSON Schema (unknown properties, shape, required fields, enums, page bounds, typed-literal compatibility);
3. strict typed construction (`JsonUnmappedMemberHandling.Disallow`), including range checks for numeric literals and canonical/parseable checks for typed temporal, GUID, and binary literals;
4. `CqmValidator` for catalog-independent rules: alias uniqueness/scope, alias-reference context, v1 join-condition shape, operator/function/aggregate arity, aggregate nesting/placement, `having` classification, and division by a literal zero.

Schema diagnostics filter evaluator branch noise so each reported path points at an actionable location. Valid `double`/`json` literals outside decimal range are preserved end to end; the schema evaluator receives a sanitized numeric copy only to avoid a `JsonSchema.Net` decimal-conversion limitation. Errors are collected as stable `code`/`path`/`message` values (`CqmValidationError`) instead of throwing one violation at a time.

Catalog entity/field/relationship resolution, type inference, expression normalization, group-by completeness, join-priority resolution, administrator limits, and SQL compilation are later Milestone 2 slices. No CQM document can execute until those slices exist.

## Join shape

A v1 join carries **at most one** of `on` and `relationship`. If `on` is present it wins; if `relationship` is present
it names a catalog relationship; if both are absent the resolver must later use the one unambiguous discovered or
configured relationship for the pair. Both present is rejected by the schema and by direct construction. The resolver
itself, including ambiguity errors, is a later Milestone 2 slice; structural acceptance of the automatic shape does not
mean the join is executable yet.

## Temporal literals

The wire grammar is canonical, culture-independent and exact (TS2-02):

| Type | Accepted form |
| --- | --- |
| `date` | `yyyy-MM-dd` |
| `time` | `HH:mm:ss` or `HH:mm:ss.FFFFFFF` (1–7 fractional digits) |
| `datetime` | `yyyy-MM-ddTHH:mm:ss` or `yyyy-MM-ddTHH:mm:ss.FFFFFFF` (no offset) |
| `datetimeOffset` | the `datetime` form plus a mandatory `Z` or `±HH:mm`, with optional 1–7 fractional digits |

Incomplete values, date-only datetimes, missing offsets, offset-bearing `datetime` and more than seven fractional
digits are rejected. No normalization or rounding is applied.

## Local QA closure (2026-11-02)

**Post-fix qualification:** [independent QA](./qa-check-2026-11-02T14-46-14Z.md) confirms automatic-join representation
and the main temporal corrections, but finds a dangling decimal separator accepted as a zero-digit fraction (TS3-03).
Exact-source remote CI is now green; ADR 0016 remains Proposed pending independent acceptance of residual findings.

The [previous QA](./qa-check-2026-11-02T10-54-29Z.md) found TS2-01/TS2-02; their main cases have contract fixes and
regression tests, subject to the residual above. Structural success is still not catalog/type/operational validation:
disconnected joins and expensive scans must be rejected by later resolver/type/limit stages. ADR 0016 remains Proposed
until independent acceptance; the exact-source remote CI requirement is now satisfied.
