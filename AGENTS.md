# AGENTS.md

<!-- BEGIN CORE: identical in every owner repository. Edit all copies together. -->

## Core workflow

### Roles
- **Owner:** the human user. Owns product scope, the backlog, architecture approval, every development
  authorization, every merge into `main`, UAT, releases and deployments.
- **Development chat:** implements only the steps the owner explicitly authorizes and delivers them as a pull request.
- **QA sub-agent:** adversarial reviewer, started by the development chat for each review round. Same model
  and configuration as the development chat, fresh context, read-only.

### Canonical documents (`docs/`)
| File | Purpose |
| --- | --- |
| `backlog.md` | Complete owner-agreed plan: milestones, numbered steps, acceptance criteria, milestone gates, UAT checklist, carried-over findings. |
| `architecture.md` | Complete current architecture. |
| `decisions.md` | All settled decisions, one entry each, newest last. |
| `status.md` | Technical project status. **Source of truth.** Current state only, in tables, about 80 lines at most. |
| `status.html` | Owner view of the same content as `status.md`. |
| `qa-status.md` | Latest QA result per step and the table of open/deferred findings. |
| `changelog.md` | Material changes per step, including the history removed from `status.md`. |

Read `status.md`, `backlog.md` and `architecture.md` before planning or changing anything. Documentation describes
verified behaviour, not intentions. Git history is the archive: do not keep dated reports, queues or handoffs.
`status.md` never accumulates history: when a step closes, its narrative goes to `changelog.md` and `status.md`
keeps only the current position, gates, open findings, pending UAT and known limits. Write plain sentences with
normal spacing; put numbers and SHAs in table cells rather than long prose.
A repository still in analysis may have an incomplete backlog; no development starts until the steps exist.

### Phase 1 — Design (with the owner)
Scope, architecture and steps are agreed with the owner and written into `backlog.md`, `architecture.md` and
`decisions.md`. Material scope or architecture changes always return to this phase.

### Phase 2 — Development (delegated autonomy)
Starts only on an explicit owner request naming a step or a range (e.g. "step 10" or "steps 10–15").

**Session start.** Fetch `origin`. Check the previous delivery PR (merged, open, closed) and the required CI on the
current `main` head; record both in `status.md`. Review open Dependabot PRs: close with a written reason those that
are incompatible with the supported runtime/toolchain, list the others (and any with red CI) in the delivery report;
the owner validates and merges them. Then create the range branch `dev/<first>-<last>` from `origin/main`, or from
the still-open previous delivery branch (stacked PR, stated in the report).

For each step, in order:

1. **Carried-over first.** Resolve the step's `Carried over` findings, or re-defer each with a written reason.
2. **Implement** in the project source tree, with tests in the existing test tree (do not rename project folders).
   Change only the step's scope; never alter unrelated user work.
3. **Run the local gates** listed under *Repository specifics*: the fast tier always, targeted slow tests only for
   the areas the step changed (see *Test tiers and CI*). Record pass/fail/skip counts.
4. **Commit the candidate** on the range branch and record its full SHA.
5. **Adversarial QA.** Start a new QA sub-agent: same model and configuration, fresh context, read-only, on the
   exact candidate SHA. Give it the step's acceptance criteria, repository rules and gate commands — not the
   developer's conclusions. It reruns gates and probes failure paths; it does not edit, commit or push.
   If the same model is unavailable, STOP and ask; never substitute silently.
6. **Fix loop.** Fix confirmed P0/P1 findings with regression tests, commit, start a **new** QA sub-agent on
   the new SHA. Maximum **3 QA rounds per step**: a P0/P1 still open after round 3 means STOP and ask the owner.
7. **Defer** P2/P3 findings into the `Carried over` section of `backlog.md`, under the next step (ID, severity,
   origin step, SHA, short description), and into `qa-status.md`. Never rely on chat memory for them.
8. **Owner checks become UAT.** Anything only the owner can judge (playtest, visual approval, real devices,
   assistive technology, product acceptance) is added to the milestone's `UAT` checklist in `backlog.md` and the
   step closes on technical acceptance as `DONE — UAT pending`. Stop earlier only when a later step truly depends
   on an owner choice (for example approving a design proposal before building it).
9. **Close the step:** update `status.md`, `status.html`, `qa-status.md`, `changelog.md`, `backlog.md` and, when
   relevant, `architecture.md` / `decisions.md`; commit. Continue with the next authorized step.

When the last step of a milestone closes, the delivery report presents its UAT checklist. UAT problems reported by
the owner become fix steps in `backlog.md`.

### Severity
| Level | Meaning | Effect |
| --- | --- | --- |
| P0 | Data loss, security breach, crash/unusable core path | Blocks the step |
| P1 | Wrong behaviour on a required path, missing required test/criterion | Blocks the step |
| P2 | Limited-impact defect, robustness or coverage gap | Deferred to next step |
| P3 | Cosmetic, documentation, minor cleanup | Deferred to next step |

### Test tiers and CI
| Tier | Content | When |
| --- | --- | --- |
| Fast | Build, lint, unit, fast integration, a few smoke E2E. Minutes, not tens of minutes. | Every step locally; CI on every PR and on `main`, docs-only changes included. |
| Targeted | E2E/UI, integration or performance tests for the area the step changed. | Locally in the step and by the QA sub-agent, only when that area changed. |
| Milestone gate | Explicit cases listed in the milestone's `Gates` table in `backlog.md` (full E2E/UX suite, cross-browser, performance budgets, devices). | Explicit step at milestone end, or when the owner asks; local run or manual CI workflow. |

Never weaken a test to keep CI fast: move it to the right tier. Timing-sensitive tests do not belong in the fast tier.
*Repository specifics* name the commands of each tier.

### Delivery and STOP
- STOP when the authorized range is complete, when the owner ends the session, after a non-converging QA loop,
  or when scope/architecture changes, credentials, paid resources or real data are needed.
- **Deliver as a pull request.** Push the range branch and open (or update) a PR to `main`. The PR description is
  the STOP report: steps done, final SHA, gate counts, QA rounds and verdicts, deferred findings, pending UAT,
  Dependabot notes, next step. Wait for the PR's required CI and record the result. A real CI failure is fixed on the
  same branch (new QA round if it changes code or tests); if it cannot be fixed, say so in the PR. Then STOP.
- **The owner's merge is the approval.** Owners merge with *Create a merge commit* so reviewed SHAs stay on `main`.
  The development chat never pushes to `main`, merges, force-pushes or rewrites shared history.
- Owner-requested fixes re-enter the commit / adversarial-QA loop on the same or a new range branch.
- CI success is not QA. Tags, releases and deployments need their own explicit approval.

### Evidence
QA and gate evidence (logs, TRX/coverage, probes, reports) goes under `artifacts/<step>/` at the repository root,
which is git-ignored. Keep only what is needed for open work: delete a range's evidence once its PR is merged
with green CI, and never let evidence fill the VM disk. `status.md`/`qa-status.md` keep the counts and verdicts.

### General rules
- Language: English for AGENTS.md and agent-facing docs; `status.html` in English.
- No real customer data, cloud provisioning, paid APIs or new credentials without owner approval.
- Use bounded probes (time/memory limits) on the shared VM.
- `docs/workflow-playbook.md` is the portable description of this workflow (new-project setup, migration,
  templates, rationale). It is identical in every owner repository; update it together with this block.

<!-- END CORE -->

## Repository specifics — thesqlodatamcp

**Product.** Self-hosted, **read-only** data gateway for AI assistants and HTTP clients: MCP (Streamable HTTP), a
read-only OData 4.01 profile, a versioned JSON query API and HTTP `QUERY`/`POST`. Every adapter translates into one
typed Canonical Query Model (CQM); the SQL Server provider emits one parameterized `SELECT` with catalog-only
identifiers under a least-privilege read-only identity. The authoritative product baseline is
`docs/AI_DATA_GATEWAY_HANDOFF.md`; the v1 plan is `docs/backlog.md`. Post-v1 features are never pulled into v1.

**Decisions.** `docs/decisions.md` is the decision log to read; detailed ADRs live in `docs/decisions/NNNN-*.md`.
Never rewrite an accepted ADR: supersede it with a new one and add the row.

**Layout.** `src/` (`TheSqlODataMcp.Core`, `.Persistence`, `.Protocols`, `.SqlServer`, `.Web`), `tests/`
(per-project tests, `IntegrationTests` with real SQL Server, `Probe` child-process resource oracles), `spikes/`
(library spikes and the SQL Server test fixture), `eng/verify-markdown-links.sh`. CQM contract: `docs/cqm.md`.

**Stack.** .NET 10, SDK pinned in `global.json` (10.0.110, `latestPatch`): if that SDK band is not installed, commands
run inside the repository fail SDK resolution — install it or report the limitation, do not edit `global.json`.
Docker for the real SQL Server tests; check `docker info` and free disk before relying on them locally.

**Local gates** (repository root):

```bash
dotnet restore thesqlodatamcp.slnx
dotnet build thesqlodatamcp.slnx --no-restore                 # warnings are errors
dotnet test thesqlodatamcp.slnx --no-build --no-restore --filter "Category!=SqlServerIntegration"
dotnet format thesqlodatamcp.slnx --verify-no-changes --no-restore
bash eng/verify-markdown-links.sh                            # every Markdown link in the repo must resolve
# real SQL Server (Docker required):
dotnet test spikes/platform/sqlserver-tests/SqlServerTests.ApiSpike.csproj --filter "Category=FixtureStatic"
dotnet test tests/TheSqlODataMcp.IntegrationTests/TheSqlODataMcp.IntegrationTests.csproj --filter "Category=SqlServerIntegration"
```

If Docker is unavailable locally, say so: the SQL Server jobs then run only in CI and a skipped local run is not evidence.

**Required CI** (`.github/workflows/ci.yml`, workflow `CI`): jobs `validate` and `sqlserver-integration`, green on
the delivery PR head and on `main` after merge.

**Product boundaries — never break:**
- Read-only: no write path, no raw SQL or SQL fragments from callers, exactly one parameterized `SELECT` per operation.
- Identifiers come only from the catalog, never from request text.
- Public adapters (`Protocols`, `Web`) see the catalog only through `CatalogReadView` (enforced by `CatalogBoundaryTests`).
- Semantic Markdown is data, never injected into tool descriptions or server instructions.
- Input limits (`CqmLimits`, overlay depth/size/node limits) are enforced before parsing/evaluation; resource tests run
  in a child process with hard memory and time limits.
- No host code (SQL-07 onward) before checkpoint C is accepted; no anonymous data exposure while auth is incomplete.
- No real customer data, cloud resources or paid services in tests.
