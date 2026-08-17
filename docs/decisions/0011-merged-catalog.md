# ADR 0011 — Merging the semantic overlay into the technical catalog

- **Status:** Proposed
- **Date:** 2026-08-17

## Context

ADR 0010 accepted import and strict, isolated validation of an administrator-authored semantic overlay (Markdown narrative plus YAML metadata), deliberately deferring the actual merge with the physical `TechnicalCatalog` to a later slice. The handoff's merge rules (`docs/AI_DATA_GATEWAY_HANDOFF.md` §8.4–8.5) require: physical objects located by `source`; names/descriptions/aliases/warnings overlaying (not replacing) physical metadata; YAML relationships adding to, not replacing, FK-discovered relationships; YAML winning on an explicit override; invalid structural references preventing activation; and keyless views not becoming ordinary OData entity sets until YAML supplies a logical key, without synthesizing one from all columns.

This slice is bounded to producing the merged, in-memory view and its deterministic canonical form. It does not implement persistence, revision/activation/rollback, capability models, or any protocol-adapter behavior that consumes the merged catalog — those are separate, later Milestone 1 backlog items.

## Decision

- Add `MergedCatalog`, `MergedEntity`, `MergedField`, `RelationshipProvenance`, and `MergedRelationship` to `TheSqlODataMcp.Core`, following `TechnicalCatalog`'s/`SemanticOverlay`'s construction-time invariant discipline. Each `MergedEntity`/`MergedField` composes the untouched physical object (`Physical`) alongside overlay-derived annotations, rather than duplicating physical properties — the physical object is never lost, satisfying "discovered metadata remains inspectable."
- Add `CatalogMerger.Merge(TechnicalCatalog, SemanticOverlay?)`, returning a `CatalogMergeResult` (mirroring `SemanticOverlayImportResult`'s shape and reusing `SemanticOverlayValidationError`). Merging with no overlay always succeeds (`Configured = false`, physical-only fallback values), matching the handoff's "`configured: false` when absent" behavior.
- When an overlay is supplied, re-validate its physical references against the *specific* `TechnicalCatalog` passed to this call — never assume it is the same instance the overlay was originally imported against, since a caller may re-introspect and pass a refreshed catalog. This re-validation is a fresh pass over the already-typed `SemanticOverlay` object (intentionally not sharing code with `SemanticOverlayImporter`'s DTO-based import-time validation) and adds one check ADR 0010 explicitly deferred: `overlay.CatalogVersion` must exactly (ordinal) match `catalog.CatalogVersion`, or the merge fails closed (`merge.catalogVersionMismatch`). It also adds `merge.oDataKeyFieldNotFound`, validating that overlay `odata.key` field names resolve to real physical fields — a check ADR 0010 deliberately did not perform, since the key is only consumed here. All seven merge error codes are collected in one pass rather than failing at the first.
- Settled precedence rules:
  - Effective display name: overlay `displayName`, else overlay `name`, else the physical object name (whitespace-only overlay values are treated as absent, not passed through — see "defect found and fixed" below).
  - Effective description: overlay description, else the physical description — overlay *layers onto*, never blanks out, physical metadata.
  - Effective key (`EffectiveKeyFields`): overlay `odata.key` when declared **always wins**, even over an existing physical primary key — a deliberate "YAML wins on an explicit override" interpretation of handoff rule 5, not limited to keyless views. Otherwise the physical primary key's fields; otherwise empty (keyless — never synthesized from all columns, per §8.5).
  - Relationships: the physical, FK-discovered `CatalogRelationship`s (tagged `Discovered`) plus the overlay's declared relationships (tagged `Configured`) are a union, never a replacement.
  - `Exposed`/`OData` never filter `MergedCatalog.Entities` — every physical entity always appears, annotated; protocol-level filtering is explicitly out of scope for Core.
- Add `MergedCatalogCanonicalJson`, mirroring `TechnicalCatalogCanonicalJson`'s style and determinism guarantees (ordinal-sorted entities/fields/relationships, camelCase, lowercase-hex SHA-256), sensitive to every overlay-attributable field.

## Defect found and fixed during review

Independent review found two ways malformed-but-schema-legal overlay content could crash `CatalogMerger.Merge` with an unhandled exception instead of returning a graceful `CatalogMergeResult.Failure` — violating this slice's own stated design principle:

1. A whitespace-only overlay `displayName`/`name` reached `MergedEntity`'s constructor, which rejects whitespace-only display names via `TechnicalCatalog.RequireIdentifier`. Fixed by treating whitespace-only overlay values as absent in the fallback chain (`IsNullOrWhiteSpace` instead of `IsNullOrEmpty`), so it correctly cascades to the next candidate and ultimately the always-valid physical object name.
2. A whitespace-only overlay relationship YAML key (its name) reached `MergedRelationship`'s constructor and threw the same way. ADR 0010's schema does not forbid this — it validates property *values*, not property *names* — so this is reachable from real overlay input, not just a directly-constructed test double. Fixed with a new merge-time check, `merge.relationshipNameInvalid`.

A related, more severe defect was found in already-accepted ADR 0010 code while stress-testing this slice's new test suite for reliability (see ADR 0010's subsequent-evidence note): a thread-safety race in `SemanticOverlayImporter`'s shared JSON Schema evaluation, measured at ~42% silent validation bypass under concurrent load, fixed by serializing evaluation with a lock.

## Acceptance evidence

Delegated implementation (dev-senior sub-agent, Sonnet-5) added the domain model, merger, canonical JSON, and 28 tests (18 construction-invariant/canonical-JSON tests, 10 merge-behavior tests including a defense-in-depth test that imports an overlay against one catalog and merges it against a trimmed second catalog, proving all five re-validation error codes are independently reachable in one call).

Independent primary-agent review covered the full diff, traced the composition-over-duplication design for `MergedEntity`/`MergedField`, verified the canonical JSON's sort keys and wire shape against `TechnicalCatalogCanonicalJson`'s precedent, and found the two whitespace-crash defects above by reading `CatalogMerger.BuildEntity`/`BuildRelationships` against what schema-level validation actually guarantees (rather than what it was assumed to guarantee). The primary agent fixed both directly, added four regression tests (`CatalogMergerQaTests.cs`) proving the crashes no longer occur and the relationship case now fails cleanly with `merge.relationshipNameInvalid`, and separately found, fixed, and added a permanent regression test for the ADR 0010 thread-safety defect above.

Local validation on 2026-08-17: `dotnet build thesqlodatamcp.slnx`: 0 warnings, 0 errors, all 9 projects. `dotnet test tests/TheSqlODataMcp.Core.Tests/...`: 77 passed (44 pre-existing + 28 delegated + 1 thread-safety regression + 4 whitespace-crash regressions = 77). `dotnet test thesqlodatamcp.slnx --filter "Category!=SqlServerIntegration"`: Core.Tests 77, SqlServer.Tests 94, ProtocolTests 1, IntegrationTests 4, all passed, repeated across five full-solution runs with no flakes (the thread-safety fix was verified separately with a 2,000-iteration concurrent stress probe outside the normal test suite: 0/2,000 failures after the fix, versus ~42% before). `dotnet format --verify-no-changes`: fails only on the same pre-existing local `core.autocrlf` artifact documented in ADR 0009/0010 (none of the eight new/touched files appear in the output).

This slice requires no real SQL Server access. Its only outstanding gate is a green `validate` job on `origin/main`; this ADR remains Proposed until that run is confirmed.

## Consequences

- `TechnicalCatalog` and `SemanticOverlay` remain independently valid, reusable objects; `MergedCatalog` is a derived, read-only view that never mutates either.
- Future CQM/protocol work can consume `MergedEntity.EffectiveKeyFields`/`Relationships`/`Exposed`/`OData` without re-deriving merge precedence, while still reaching the untouched physical object via `.Physical` when needed.
- No persistence, revision/activation/rollback, capability model, or search index exists yet for `MergedCatalog`; a failed merge does not yet "preserve the last valid revision" in any durable sense, since there is no durable store to preserve it in — that is explicitly the next Milestone 1 work.
- `SemanticOverlayImporter`'s schema evaluation is now serialized; if catalog-overlay import volume ever becomes a real throughput concern (unlikely for an administrative operation), revisit with a per-call schema instance or a verified-thread-safe library version instead of a lock.
