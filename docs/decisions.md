# Decisions — thesqlodatamcp

Single decision log to read, newest last. Detailed records (context, options, consequences) live in
[`decisions/`](decisions/0001-project-identity.md). Accepted ADRs are not rewritten: a changed decision gets a new ADR that
supersedes the old one and a new row here. The product baseline remains [the handoff](AI_DATA_GATEWAY_HANDOFF.md).

## Workflow decisions

| ID | Date | Decision | Consequence |
| --- | --- | --- | --- |
| D-01 | 2026-11-17 | Development workflow aligned across owner repositories (`AGENTS.md` core): design with the owner, delegated step/range development, per-step adversarial QA sub-agent with the same model, P0–P3 severity, delivery as a PR per range, UAT at milestone end. | Replaces the technical-lead skill, the fixed dev-senior sub-agent, `development-state.md`, dated QA reports and work queues (all in Git history). |

## Architecture decisions

| ADR | Date | Decision | Status |
| --- | --- | --- | --- |
| [ADR 0001](decisions/0001-project-identity.md) | 2026-07-18 | Project identity and repository continuity | Accepted |
| [ADR 0002](decisions/0002-dotnet-identifiers.md) | 2026-07-19 | .NET solution and identifier naming | Accepted |
| [ADR 0003](decisions/0003-protocol-identity-catalog-libraries.md) | 2026-07-19 | Protocol, identity, and catalog library baseline | Accepted |
| [ADR 0004](decisions/0004-sqlserver-test-infrastructure.md) | 2026-07-21 | Disposable SQL Server test infrastructure | Accepted |
| [ADR 0005](decisions/0005-solution-build-and-ci-baseline.md) | 2026-07-20 | Solution, build, and CI baseline | Accepted |
| [ADR 0006](decisions/0006-technical-catalog-core-model.md) | 2026-07-21 | Provider-neutral technical catalog core | Accepted |
| [ADR 0007](decisions/0007-sqlserver-type-mapping.md) | 2026-07-21 | SQL Server catalog type mapping | Accepted |
| [ADR 0008](decisions/0008-sqlserver-introspection-foundation.md) | 2026-07-22 | SQL Server introspection foundation | Accepted |
| [ADR 0009](decisions/0009-sqlserver-relational-metadata-introspection.md) | 2026-07-23 | SQL Server relational metadata introspection | Accepted |
| [ADR 0010](decisions/0010-semantic-overlay-import-validation.md) | 2026-08-17 | Semantic overlay Markdown/YAML import and validation | Accepted |
| [ADR 0011](decisions/0011-merged-catalog.md) | 2026-08-17 | Merging the semantic overlay into the technical catalog | Accepted |
| [ADR 0012](decisions/0012-catalog-revision-lifecycle.md) | 2026-08-22 | Catalog revision lifecycle model | Accepted |
| [ADR 0013](decisions/0013-catalog-revision-persistence.md) | 2026-09-06 | SQLite/EF Core control store and durable `CatalogRevisions` storage | Accepted |
| [ADR 0014](decisions/0014-catalog-activation-and-bootstrap-modes.md) | 2026-09-06 | Atomic catalog activation, last-valid rollback, and bootstrap modes | Accepted |
| [ADR 0015](decisions/0015-catalog-deserialization-and-search-index.md) | 2026-09-06 | Catalog JSON→domain deserialization and in-memory search index | Accepted |
| [ADR 0016](decisions/0016-cqm-v1-contract.md) | 2026-10-28 | CQM v1 contract and strict structural validation | Accepted (bounded structural slice, 2026-11-04) |

## Pending owner decisions

SQL-00 v1 support matrix (five items with recommended defaults): see [backlog.md](backlog.md#sql-00--v1-support-matrix--blocked-owner-decisions).
