# Status — thesqlodatamcp

Updated: **2026-11-17**. Source of truth for agents; [status.html](status.html) is the owner view of the same content.
Current state only; history is in [changelog.md](changelog.md).

## Current position

| Item | Value |
| --- | --- |
| Milestone | 2 — CQM and SQL Server query engine |
| Last implemented work | Phase A′ (SQL7-01…04, SQL-A5, SQL-A6a) and SQL-01c, on `main` |
| Pending QA | Independent adversarial QA of Phase A′ + SQL-01c has not run yet |
| Next steps | SQL-01b (no owner dependency); SQL-01a and SQL-01d after the SQL-00 decisions |
| Owner decision needed | SQL-00 v1 support matrix, five items with recommended defaults ([backlog](backlog.md)) |
| Authorized work | None. Waiting for the owner. |
| `main` on GitHub | `4df04ac` — CI SUCCESS |
| Open PRs | Governance PR from `dev/governance-2026-11-17` (workflow alignment) |

## Gates (last verified)

| Gate | Result | SHA |
| --- | --- | --- |
| Build | warning-free | `89bbcef` (local, developer) |
| Non-SQL tests | 379 pass (Core 243, Persistence 37, SqlServer 94, Integration 4, Protocol 1) | `89bbcef` (local, developer) |
| Formatter, Markdown links | pass | `89bbcef` |
| Real SQL Server tests | not run locally (no Docker at the time); covered by CI | — |
| CI `CI` (validate + sqlserver-integration) | SUCCESS, [run 42631654978](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/42631654978) | `4df04ac` |

## Steps

| Step | State |
| --- | --- |
| Milestones 0 and 1 | DONE |
| CQM v1 structural slice (ADR 0016), Phase A (SQL-A1…A4) | DONE (independent QA 2026-11-04 and 2026-11-09) |
| Phase A′ (SQL7-01…04, SQL-A5, SQL-A6a), SQL-01c | IMPLEMENTED_PENDING_QA |
| SQL-00 v1 support matrix | BLOCKED — owner decisions |
| SQL-01a, SQL-01d | PLANNED — blocked by SQL-00 items 4 and 5 |
| SQL-01b | PLANNED — can start |
| SQL-02a…02d (checkpoint C) | PLANNED |
| SQL-03…06 (query engine) | PLANNED |
| SQL-07…17 (host, OAuth, adapters, release) | PLANNED — no host code before checkpoint C |

## Open findings

| ID | Severity | Target | Summary |
| --- | --- | --- | --- |
| TS-02 | P1 | SQL-01a | Schema/object filters must be applied SQL-side and validated at startup. |
| TS-06 | P1 | SQL-07 | Proxy trust list, empty admin token denial, MCP Origin policy. |
| TS-01 wiring | P1 | SQL-08/12/13 | Wire `CatalogReadView` into every public surface. |
| TS-05 | P2 | SQL-01b | Unusual metadata diagnostics. |
| REL-NAME, ADR-0014-LIM, DCR | P2 | SQL-04, SQL-07, SQL-10 | Design risks recorded in ADRs 0011, 0014, 0003. |
| TS-08/09/10 | P3 | SQL-15 | Triaged design/automation debt. |

These are planned gates for future steps, not regressions. Details in [qa-status.md](qa-status.md).

## Pending UAT (owner)

None.

## Known limits

- Milestone 2 is incomplete: no resolver, compiler, executor, host, OAuth or protocol adapters yet.
- The published CQM schema is a client contract; the server verdict is authoritative (some evaluators accept
  out-of-range `int64` literals that the server rejects).
