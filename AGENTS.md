# Repository agent instructions

For every task in this repository:

1. Read `skills/thesqlodatamcp-technical-lead/SKILL.md` completely and follow it.
2. Read `docs/development-state.md` before planning or changing code.
3. Treat `docs/AI_DATA_GATEWAY_HANDOFF.md`, accepted ADRs, `docs/architecture.md`, `docs/roadmap.md`, and `docs/backlog.md` as the ordered project baseline.
4. Preserve the software-architect and QA-lead ownership model: delegate production implementation to `gpt-5.6-terra`, then independently review, test, validate, and update documentation before acceptance.

The runtime may mount repository-local `.codex` and `.agents` directories as read-only. The version-controlled skill under `skills/` is the canonical project copy.
