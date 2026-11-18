# Workflow playbook

Portable description of the owner's project workflow. It is **identical in every owner repository** and is meant
to be reused: to start or migrate another project, tell its agent "take the workflow from `<this repo>`", and it
reads this file plus the `BEGIN CORE`/`END CORE` block of `AGENTS.md`.

It does not depend on any agent tool (opencode, Claude Code, Codex, plain chat). The **rules** live in the
`AGENTS.md` core block; this file explains how to set them up, gives templates and records why they exist.
Update this file and the core block in all repositories together.

## 1. What is portable

| Piece | Where | Reuse |
| --- | --- | --- |
| Rules | `AGENTS.md`, between `<!-- BEGIN CORE ... -->` and `<!-- END CORE -->` | Copy **verbatim** |
| Playbook | `docs/workflow-playbook.md` (this file) | Copy **verbatim** |
| Repository specifics | `AGENTS.md`, after the core block: stack, gate commands, CI, product boundaries | Rewrite per project |
| Canonical docs | `docs/backlog.md`, `architecture.md`, `decisions.md`, `status.md`, `status.html`, `qa-status.md`, `changelog.md` | Create from templates (§5) |
| CI | `.github/workflows/`, Dependabot config | Adapt per stack |

## 2. The flow in one page

1. **Design (owner + chat).** Agree scope, architecture and a numbered list of steps grouped into milestones.
   Output: `backlog.md`, `architecture.md`, `decisions.md`. Nothing is developed before the steps exist.
2. **Authorization.** The owner names a step or a range: "procedi con step 4–10".
3. **Session start.** The development chat fetches, checks the previous PR and the CI on `main`, triages
   Dependabot, creates branch `dev/<first>-<last>`.
4. **Per step loop.** Carried-over findings first → implement + tests → local gates (fast tier always, targeted
   slow tests only for what changed) → commit → adversarial QA
   sub-agent (same model and configuration, fresh context, read-only) → fix P0/P1 and re-run QA, **max 3 rounds** →
   defer P2/P3 into `backlog.md` *Carried over* and `qa-status.md` → owner-only checks into the milestone *UAT*
   checklist, step `DONE — UAT pending` → update docs, commit → next step.
5. **Delivery.** Push the range branch, open a PR whose description is the STOP report, wait for PR CI, STOP.
6. **Owner review.** The owner merges with *Create a merge commit* (merge = approval), or asks for fixes, which
   re-enter the loop. The chat never pushes to `main`, merges or force-pushes.
7. **Milestone end.** An explicit gate step runs the milestone `Gates` cases (full E2E/UX, performance, …); the
   owner runs the UAT checklist. Problems become fix steps in `backlog.md`.
8. **Housekeeping.** Evidence in git-ignored `artifacts/<step>/` is deleted after merge with green CI; merged
   `dev/*` branches are deleted.

## 3. Starting a new project

1. Create the repository, `docs/`, a `.gitignore` containing `artifacts/`, and `AGENTS.md` = title + one-paragraph
   project summary + core block + *Repository specifics* (template §5.1).
2. Design session with the owner. Ask until these are clear: goal and users; in/out of scope; stack and runtime
   versions; supported platforms; data, security and cost constraints; milestones; what "done" means per step
   (acceptance criteria and tests); which checks only the owner can do (become UAT). Record choices in
   `decisions.md` as they are made.
3. Write `backlog.md` (all milestones and steps, each with acceptance criteria), `architecture.md`, initial
   `status.md`/`status.html`, empty `qa-status.md`, `changelog.md`. Owner reviews and approves.
4. Set up CI on the **fast tier** only (same commands as locally, runs on every PR and push), a manual workflow
   (`workflow_dispatch`) for milestone-gate suites if they need CI, branch protection on `main` if wanted, Dependabot.
5. First development request from the owner. From here the core block governs.

A repository still in analysis may stop at step 2–3 with an incomplete backlog; no development starts until the
steps exist.

## 4. Migrating an existing project

1. Inventory the docs: anything describing state, plans, QA, handoffs, queues or roadmaps.
2. Map it onto the canonical set: plan → `backlog.md`; architecture → `architecture.md`; ADR-like notes →
   `decisions.md` (a rich ADR folder may stay, with `decisions.md` as its index); current state → `status.md`
   (tables, ≤ ~80 lines); history → `changelog.md`; open QA findings → `qa-status.md` and backlog *Carried over*.
3. Delete dated reports, ledgers, handoffs and agent-tool-specific instructions; Git history is the archive.
   Fix every link that pointed to deleted files.
4. Replace `AGENTS.md` with core block + rewritten repository specifics. No model names in `AGENTS.md`.
5. Add `artifacts/` to `.gitignore`. Archive obsolete long-lived branches with a tag before deleting them.
6. Deliver the migration itself as a PR, like any other change.

## 5. Templates

### 5.1 `AGENTS.md`

```markdown
# AGENTS.md — <project>

<One paragraph: what the project is, current milestone.>

<!-- BEGIN CORE ... -->   (copied verbatim)
<!-- END CORE -->

## Repository specifics
- Stack and pinned versions: ...
- Test tiers (exact commands): fast (local + CI on every PR/push), targeted (per changed area), milestone gate.
- CI: workflows and required jobs; CI must be green on the PR head and on `main` after merge.
- Product boundaries / never-do list: ...
- Where decisions live (`docs/decisions.md`, plus ADR folder if any).
```

### 5.2 `backlog.md`

```markdown
# Backlog — <project>
Goal and scope (in / out). Step states: PLANNED → IN_PROGRESS → IMPLEMENTED_PENDING_QA → DONE
(or DONE — UAT pending); also BLOCKED, NOT_NEEDED, DEFERRED_BY_OWNER (with reason).

## Milestone M1 — <name>
| Step | Title | Acceptance criteria | State |
| --- | --- | --- | --- |
| P-01 | ... | testable criteria | PLANNED |

## Gates — Milestone M1
| Gate | Command / procedure | State |

## UAT — Milestone M1
| Item | From | State |

## Carried over
- **<next step>:** <ID> · <P2/P3> · <origin step> · `<sha>` · <description>
```

### 5.3 `status.md` (current state only, tables, ≤ ~80 lines)

Sections: *Current position* (milestone, authorized range, range state, last closed step, owner decision needed,
`main` SHA + CI, current delivery PR, Dependabot), *Gates (last verified)* (gate · result · SHA), *Steps*
(step · state), *Open findings* (pointer to `qa-status.md`), *Pending UAT*, *Known limits*. `status.html` shows the
same content for the owner, in English.

### 5.4 `qa-status.md`

```markdown
| Step | Reviewed SHA | Rounds | Verdict | Notes |
## Open / deferred findings
| ID | Severity | Origin | SHA | Target step | Description | State |
```

### 5.5 `decisions.md`

```markdown
| ID | Date | Decision | Consequence |
```
Newest last; superseded entries stay with a pointer to the replacement.

### 5.6 `changelog.md`

Newest first, one section per step or delivery: what changed, SHAs, QA verdict, links to the PR.

### 5.7 PR description = STOP report

```markdown
## Range <first>–<last> — STOP report
| Step | State | Final SHA | Gates (pass/fail/skip) | QA rounds / verdict |
Deferred findings (ID · severity · target step) · Pending UAT · Dependabot notes ·
CI on this PR · Stop reason · Proposed next step / owner decisions needed
```

### 5.8 QA sub-agent brief

Give the sub-agent: repository path and exact candidate SHA; the step's acceptance criteria from `backlog.md`;
the core rules and repository specifics; the gate commands; the severity table. Instruct it to work read-only,
rerun the gates itself, probe failure paths and edge cases, and return a verdict (`PASS`, `PASS_WITH_MINORS`,
`FAIL`) with findings as ID · severity · file/line · reproduction. Do **not** give it the developer's conclusions.

## 6. Owner cheat sheet

| Want | Say / do |
| --- | --- |
| Plan a project | "progettiamo <X>": design session, then approve backlog/architecture |
| Develop | "procedi con step 4–10" (or one step) |
| Accept a delivery | Review the PR, merge with *Create a merge commit* |
| Reject / change | Comment or tell the chat; fixes re-enter the QA loop |
| Run UAT | At milestone end, work through the *UAT* table in `backlog.md` |
| Reuse the workflow elsewhere | "prendi il flusso da <this repo>": agent reads this file + core block |

## 7. Why these rules

| Rule | Reason |
| --- | --- |
| Design before code, explicit step authorization | Owner keeps scope control; long ranges can run unattended. |
| Adversarial QA, same model, fresh context | Fresh eyes without inherited assumptions; same capability as the author. |
| Fast CI, slow suites by area and at milestone end | Waiting tens of minutes for hundreds of UI tests on every unrelated change wastes time and CI minutes. |
| Max 3 QA rounds | A non-converging loop signals a design problem the owner must see. |
| P2/P3 written into the backlog | Chat memory is lost between sessions; written findings are not. |
| Owner checks as UAT at milestone end | The owner cannot test by hand inside a long range. |
| PR per range, owner merges | Review happens on the server, from any device; `main` only changes with approval. |
| Merge commits | Reviewed SHAs stay reachable on `main`. |
| `status.md` short, history in changelog | Agents read status first; long ledgers waste context and drift. |
| Evidence git-ignored and deleted after merge | Small VM disk; Git keeps the counts, not the logs. |
| No model names in `AGENTS.md` | Models change; rules should not. |
