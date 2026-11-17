# QA status — thesqlodatamcp

Latest adversarial QA per step and the table of open or deferred findings. Severity P0–P3 as defined in
`AGENTS.md` (historical reports used critical/high/medium/low ≈ P0/P1/P2/P3). Round reports are not kept;
earlier dated reports are in Git history.

## Latest QA per step

| Scope | Reviewed SHA | Verdict | Notes |
| --- | --- | --- | --- |
| Milestone 1 foundation + QA repair gates | `f16f653` → fixes | CHANGES_REQUIRED, then fixed | TS-03/04/07, TS2-01/02, TS3-01/02/03 closed with regressions. |
| CQM structural slice (ADR 0016) | `2f63e58` | PASS | 359 tests incl. 2 real SQL integration; exact-source CI. |
| Phase A (SQL-A1…A4) | checkpoint A (`85d7eca` + docs) | PASS with findings | Returned SQL7-01 (medium) and SQL7-02/03/04 (low), all fixed in `89bbcef`. |
| Phase A′ (SQL7-01…04, SQL-A5, SQL-A6a) and SQL-01c | `89bbcef`, `4df04ac` | **Not yet reviewed** | First adversarial QA to run with the next authorized range. |

## Open and deferred findings

| ID | Severity | Origin / SHA | Description | Target step | State |
| --- | --- | --- | --- | --- | --- |
| TS-02 | P1 | QA 2026-11-02 / `dbe98fb` | `IncludedSchemas`/`ExcludedObjects` must be SQL-side introspector options with cross-boundary FK behaviour and startup validation. | SQL-01a | OPEN (planned gate) |
| TS-06 | P1 | QA 2026-11-02 / `dbe98fb` | Proxy trust list, empty-admin-token denial, MCP Origin policy, startup validation. | SQL-07 | OPEN (planned gate) |
| TS-01 wiring | P1 | QA 2026-11-02 / `dbe98fb` | `CatalogReadView` must back every public catalog/query surface. | SQL-08/12/13 | OPEN (boundary enforced by SQL-01c) |
| TS-05 | P2 | QA 2026-11-02 / `dbe98fb` | Unusual metadata diagnostics and named warnings. | SQL-01b | DEFERRED |
| REL-NAME | P2 | ADR 0011 audit 2026-08-22 | Overlay vs FK relationship name collision needs a rule. | SQL-04 | DEFERRED |
| ADR-0014-LIM | P2 | ADR 0014 | Unbounded active-revision materialization; single-caller bootstrap. | SQL-07 | DEFERRED |
| DCR | P2 | ADR 0003 spike | No RFC 7591 registration in OpenIddict; needs a bounded, tested endpoint. | SQL-10 | DEFERRED |
| TS-08/09/10 | P3 | QA 2026-11-02 / `dbe98fb` | Ordinal hash policy, dotted identifiers, supply-chain automation. | SQL-15 | DEFERRED |
