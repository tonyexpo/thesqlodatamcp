# ADR 0001 — Project identity and repository continuity

- **Status:** Accepted
- **Date:** 2026-07-18

## Context

The authoritative architecture handoff used “AI Data Gateway” as a working title and left the final product and repository name open. The existing public repository already contained the historical `thesqlodatamcp` proof of concept and its project handoff.

## Decision

- The definitive public product name is **`thesqlodatamcp`**.
- The existing public GitHub repository is retained as the canonical repository.
- The repository is rebaselined in place; its history is not discarded.
- The legacy proof of concept remains preserved by `legacy-poc-final-2026-07-18` and Git history.
- The project is licensed under Apache License 2.0.
- “AI Data Gateway” remains a descriptive architecture label in the original handoff, not a competing product name.

The exact PascalCase convention for .NET solution, project, assembly, and namespace identifiers will be selected when the solution is scaffolded. Those technical identifiers must map unambiguously back to the public name `thesqlodatamcp`.

## Consequences

- New public documentation, packages, releases, container names, and user-facing surfaces use `thesqlodatamcp`.
- No repository migration is required.
- Historical documents remain unchanged where preserving their original context matters.
- Implementation work can reference this ADR instead of reopening product naming, repository continuity, or licensing.
