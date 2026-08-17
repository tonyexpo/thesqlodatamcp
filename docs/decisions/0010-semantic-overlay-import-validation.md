# ADR 0010 — Semantic overlay Markdown/YAML import and validation

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

ADRs 0006–0009 establish the accepted provider-neutral technical catalog and its complete SQL Server introspection, closing the physical half of Milestone 1. The handoff's catalog design (`docs/AI_DATA_GATEWAY_HANDOFF.md` §8.2–8.5) also requires an administrator-authored semantic overlay — Markdown narrative plus YAML metadata — that is validated and later merged with the technical catalog. Building the full merge in one slice would couple untrusted hand-authored input parsing with catalog-merge precedence, FK/configured-relationship combination, and keyless-view logical-key assignment in a single hard-to-review change.

This slice is bounded to import and strict validation only. It does not merge the overlay into a `TechnicalCatalog`; that is a separate future slice.

## Decision

- Add `SemanticOverlay` and related types (`SemanticOverlayEntity`, `SemanticOverlayField`, `SemanticOverlayODataSettings`, `SemanticOverlayRelationship`, `SemanticOverlayJoinFieldPair`, `SemanticOverlayCardinality`, `SemanticOverlayWarning`) to `TheSqlODataMcp.Core`, following `TechnicalCatalog`'s construction-time invariant discipline: defensive collection copies, ordinal string/identity comparison, and rejection of null/duplicate/empty-required input at the constructor.
- Add `SemanticOverlayImporter`, a static importer with two entry points — one combined Markdown-with-YAML-front-matter document, and separate YAML plus Markdown documents — that validates an overlay against a previously discovered `TechnicalCatalog`.
- Require two independent strictness stages on every import, per the accepted `spikes/platform/catalog-validation` evidence: strict typed YAML deserialization (`YamlDotNet`, camelCase, no `IgnoreUnmatchedProperties`) rejects unrecognized keys, including the six forbidden top-level sections (`metrics`, `reports`, `savedQueries`, `facts`, `dimensions`, `defaultBusinessFilters`); a versioned JSON Schema (`JsonSchema.Net`, `additionalProperties: false` throughout) independently enforces structural/cross-field rules such as required fields and the `cardinality` enum. Neither stage alone is sufficient.
- Validate physical references against the supplied `TechnicalCatalog`: every entity `source` and relationship `target` must resolve to a discovered physical entity (ordinal `schema.object` identity), every `fields` map key and join `sourceField`/`targetField` must resolve to an existing field, and duplicate entity `source` values are rejected.
- Return a result type, not throw-per-violation: `SemanticOverlayImportResult` carries either the validated `SemanticOverlay` or every independently detectable `SemanticOverlayValidationError` (stable code, JSON-pointer-style path, message) collected in one pass, so an administrator authoring YAML by hand sees every problem at once. Exceptions remain reserved for null-argument programmer errors.
- The extracted Markdown narrative is treated as opaque administrator-authored text returned verbatim and never parsed into rules, per §8.2.
- `catalogVersion` presence is validated in this slice; reconciling it against the active `TechnicalCatalog.CatalogVersion`, merging FK-discovered and YAML-declared relationships, YAML-wins override precedence, keyless-view logical keys, and merged structural hashing are explicitly deferred to the next slice.

## Acceptance evidence

Delegated implementation (dev-senior sub-agent, Sonnet-5) added the domain model, importer, versioned JSON Schema, and 22 tests. Independent primary-agent review covered the full diff line by line, including tracing every `!` null-forgiving operator in `SemanticOverlayImporter.BuildOverlay`/`BuildEntity`/`BuildRelationship` back to the schema-required fields that make each reachable-only-when-non-null, and confirming `PhysicalObjectIdentity`'s existing ordinal `Equals`/`GetHashCode` make `SemanticOverlay`'s reuse of `TechnicalCatalog.CopyDistinct` safe without a custom comparer.

The primary agent independently re-ran the full evidence suite rather than accepting the sub-agent's report at face value, including reproducing and byte-level-verifying the sub-agent's claim that `dotnet format`'s `ENDOFLINE` failures are a pre-existing local `core.autocrlf` checkout artifact (all pre-existing tracked `.cs` files, none of the six new files) and not a defect in this diff.

The primary agent then added ten independent QA tests (`SemanticOverlayImporterQaTests.cs`) targeting gaps the delegated tests did not cover: the `overlay.frontMatterMissing` error path (defined but previously unreached by any test), ordinal case-sensitivity of entity-source and field-key resolution (a product boundary emphasized since ADR 0006/0008), an unknown key directly on an entity object (as opposed to nested under `fields`), a minimal overlay with zero entities, and the domain model's own construction-time invariants (join field pairs, relationship join-collection non-emptiness, warnings, duplicate entity sources, duplicate field/relationship map keys) exercised directly rather than only through the importer, since a future merge slice may construct these types without going through YAML at all. One assertion bug in the primary agent's own first draft (`Assert.Throws<ArgumentException>` against a null argument that actually throws the derived `ArgumentNullException`) was caught by running the tests, not by inspection, and corrected.

Local validation on 2026-08-17: `dotnet restore`/`build` succeed for all projects except the pre-existing, unrelated `TheSqlODataMcp.IntegrationTests` `NU1903` advisory on `SSH.NET` (transitive via `Testcontainers.MsSql`, untouched by this diff); `dotnet test tests/TheSqlODataMcp.Core.Tests/...`: 44 passed, 0 failed (34 delegated + 10 independent QA); `dotnet test thesqlodatamcp.slnx --filter "Category!=SqlServerIntegration"`: Core.Tests 44, SqlServer.Tests 94, ProtocolTests 1, all passed.

This slice requires no real SQL Server access — it exercises no provider code — so its only outstanding gate was a green `validate` job on `origin/main`. The first push (commit `05f96e1`) failed `validate` at its very first step, `dotnet restore`, on an unrelated `NU1903` advisory that had newly appeared on a transitive dependency of the SQL Server test infrastructure (see ADR 0004's subsequent-evidence note for the full root cause and fix). After that fix (commit `9a8caf9`), GitHub Actions run [32059087651](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/32059087651) passed both `validate` (55s) and `sqlserver-integration` (67s). ADR 0010 is therefore Accepted.

## Consequences

- Administrators get complete, stable, machine-readable feedback on overlay YAML errors in one pass instead of iterative fix-and-resubmit.
- The domain model is independently safe to construct directly (not only via YAML import), which the next slice's merge logic can rely on.
- No catalog merge, activation, or persistence behavior exists yet; `TechnicalCatalog` and `SemanticOverlay` remain two separate objects until the next slice.
- The pre-existing `TheSqlODataMcp.IntegrationTests` `NU1903` restore failure (unrelated `SSH.NET` advisory) remains open and blocks a full-solution `dotnet build`/`restore`; it should be tracked and resolved independently of this catalog work.
