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

`version`, `from`, and `page` are always required. The page bounds every query explicitly; no CQM document can represent an unbounded scan.

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

## Pending adversarial findings (2026-11-02)

The [latest local QA](./qa-check-2026-11-02T10-54-29Z.md) found that current strict join construction/schema cannot
represent the handoff's automatic-relationship case (TS2-01), and permissive temporal parsers accept incomplete or
host-dependent strings (TS2-02). These are open defects, not supported capability claims. Structural success is not
catalog/type/operational validation; disconnected joins and expensive scans must still be rejected by later stages.
ADR 0016 remains Proposed and the CQM slice has no remote CI acceptance yet.
