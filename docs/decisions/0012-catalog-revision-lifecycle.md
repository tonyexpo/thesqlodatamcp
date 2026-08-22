# ADR 0012 — Catalog revision lifecycle model

- **Status:** Accepted
- **Date:** 2026-08-22

## Context

ADR 0011 accepted `MergedCatalog` but explicitly deferred "persistence, revision/activation/rollback, capability model, or search index" as the next Milestone 1 work. The backlog's next dependency-ordered item calls for "catalog capability and revision/lifecycle models alongside their first production consumer" — deliberately before SQLite persistence (a later, separate backlog item) and before atomic activation/rollback (also later and separate).

## Decision

- Add `CatalogRevisionStatus` (`Succeeded`, `Failed`) and `CatalogRevision` to `TheSqlODataMcp.Core.Catalog`, following the same construction-time invariant discipline as `TechnicalCatalog`/`SemanticOverlay`/`MergedCatalog`. A `CatalogRevision` is an immutable snapshot of *one* attempt to build a catalog at a point in time: a `CreatedAt` timestamp, the source `TechnicalCatalog`'s structural hash (`TechnicalHash`, always present, since technical discovery happens before merging), and either a `MergedCatalog` plus its own structural hash (`MergedHash`) when the merge succeeded, or the collected `SemanticOverlayValidationError`s when it failed.
- This type deliberately does **not** model which revision is currently serving, supersede an earlier revision, or persist anything. Multi-revision activation, last-valid rollback, and SQLite persistence remain later, separate Milestone 1 backlog items built on top of this type, not part of it.
- Add `CatalogRevisionFactory.Create(TechnicalCatalog, SemanticOverlay?, DateTimeOffset)` as `CatalogRevision`'s first production consumer: it runs `CatalogMerger.Merge` and computes both existing canonical-JSON structural hashes (`TechnicalCatalogCanonicalJson`, `MergedCatalogCanonicalJson`), mirroring how `CatalogMerger` was itself the first production consumer of `SemanticOverlay` in ADR 0011.
- Static success/failure factories are named `Success`/`Failure` (not `Succeeded`/`Failed`), matching the naming already established by the accepted sibling types `CatalogMergeResult` and `SemanticOverlayImportResult` — see "Defect found and fixed" below for why this specific naming matters.
- **No separate "capability" type is introduced in this slice.** This was raised explicitly with the project owner rather than inferred: the handoff's only documented "capability" concept (`get_query_capabilities`, §11) describes CQM operators/functions/limits, which belongs to Milestone 2/4 query and protocol work, not catalog foundation. The catalog-level information a reader might otherwise call "capability" (whether an overlay is configured, entity count, provider name) is already exposed by `MergedCatalog`/`TechnicalCatalog` and reachable through `CatalogRevision.MergedCatalog`; inventing a distinct `CatalogCapabilities` type now would have no concrete consumer. Revisit only when a real Milestone 2+ consumer needs a capability description this shape does not already provide.

## Defect found and fixed during review

The first draft named the static factories `Succeeded`/`Failed`, colliding with the class's own instance `bool Succeeded` property of the identical name — a hard `CS0102` "type already contains a definition" compile error that would have broken the entire `TheSqlODataMcp.Core` assembly (and, transitively, every dependent project and test). This was caught by an independent review sub-agent with no visibility into the implementer's own design reasoning, spawned per the Ultracode Dynamic Workflow policy adopted earlier this session (see `AGENTS.md` and the `thesqlodatamcp-technical-lead` skill): the primary agent wrote this slice directly rather than delegating it, so an independent, freshly-spawned reviewer was required before acceptance rather than the primary agent grading its own work. The same reviewer correctly identified that the already-accepted sibling types (`CatalogMergeResult`, `SemanticOverlayImportResult`) avoid this exact collision by using `Success`/`Failure` naming instead. Fixed by renaming both factories (`Failed` was also renamed to `Failure` for pairing consistency, though it had no colliding property) before any commit reached `origin/main`; the defect never reached CI.

## Acceptance evidence

Delegated to no one — implemented directly by the primary agent under Dynamic Workflow, then independently reviewed by a freshly-spawned sub-agent that traced every constructor call's argument order against its target signature, checked nullable-reference-type correctness against `Directory.Build.props`'s `Nullable=enable`/`TreatWarningsAsErrors=true`, and hand-traced every new test's assertions against the exact production code paths. No `dotnet` SDK was available in this environment to build or test locally.

GitHub Actions run [32593045333](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/32593045333) passed both `validate` and `sqlserver-integration` on commit `582e397`, proving the renamed `Success`/`Failure` factories build cleanly and every new `CatalogRevisionTests`/`CatalogRevisionFactoryTests` case passes for real, not just by manual trace. This slice is therefore accepted.

## Consequences

- `CatalogRevisionFactory.Create` gives future work (SQLite persistence, then an activation/rollback manager) a single, already-tested entry point that produces a fully-formed, hash-addressed revision from a technical catalog and optional overlay, without either of those future slices needing to re-derive the merge-and-hash sequence themselves.
- No persistence, multi-revision store, activation, rollback, bootstrap mode, or search index exists yet; a `CatalogRevision` currently lives only as long as its caller holds a reference to it.
- No `CatalogCapabilities` type exists yet; this is a deliberate scope decision recorded above, not an oversight, and should not be inferred as forgotten work.
