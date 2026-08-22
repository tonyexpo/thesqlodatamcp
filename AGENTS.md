# Repository agent instructions

For every task in this repository:

1. Read `skills/thesqlodatamcp-technical-lead/SKILL.md` completely and follow it.
2. Read `docs/development-state.md` before planning or changing code.
3. Treat `docs/AI_DATA_GATEWAY_HANDOFF.md`, accepted ADRs, `docs/architecture.md`, `docs/roadmap.md`, and `docs/backlog.md` as the ordered project baseline.
4. Preserve the software-architect and QA-lead ownership model, with one condition on how implementation work is assigned:
   - **Claude Code with Ultracode's Dynamic Workflow:** the static dev-senior sub-agent directive below is suspended. The primary agent owns development, independent QA, architecture, and documentation directly, dynamically assigning each piece of work — to itself or to whichever sub-agent(s) fit that specific task — instead of one fixed assignment. When the primary agent writes production code directly, a freshly spawned sub-agent with no visibility into that reasoning must independently review it before acceptance; when the primary agent instead delegates implementation to a dynamically chosen sub-agent, the primary agent performs that independent review itself. Either way, the primary agent remains fully accountable for correctness.
   - **Otherwise (Codex, or Claude Code without Ultracode):** delegate production implementation to a dev-senior sub-agent (`gpt-5.6-terra` under Codex, `Sonnet-5` under Claude Code), then independently review, test, validate, and update documentation before acceptance.

The runtime may mount repository-local `.codex` and `.agents` directories as read-only. The version-controlled skill under `skills/` is the canonical project copy.
