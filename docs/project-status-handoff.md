# Project Status & Handoff

**Status date:** 2026-07-18
**Repository:** `thesqlodatamcp` (public GitHub repository)
**Current branch:** `main`

## Agent handoff

Initial implementation work was performed with **Qwen 3.6 35B**. The active maintainer agent is now **Codex 5.6 Terra**.

This is a project-context handoff, not an attribution of every individual Git commit. It is recorded publicly so a future maintainer can understand why the implementation must be reassessed before feature work resumes.

Qwen's work established a small .NET proof of concept, but did not reach a deployable foundation. The unresolved areas started before the later SDK API hallucinations:

- no complete OAuth login or authorization design was implemented;
- the interim static bearer token is only loaded from configuration and is not real client authentication;
- integration with the `ModelContextProtocol` NuGet package was initially blocked by use of invented/non-public APIs;
- the required MCP tool surface and its compatibility strategy for future OData consumers were not designed;
- the DQL-only guarantee was attempted with a keyword blacklist, which is not sufficient as a security boundary.

The legacy repository state was therefore an **incomplete proof of concept**, not a safe SQL connector and not a release candidate. Its final committed state is now preserved by `legacy-poc-final-2026-07-18`; the obsolete executable, settings, source project, generated artifacts, and tests have been removed from `main`.

## Verified legacy state before removal

The legacy source at the time of the handoff built successfully and its unit suite reported 13 passing tests. That verification was deliberately narrow: it did **not** prove MCP protocol interoperability, tool registration, SQL connectivity, OAuth/authentication, or read-only security.

The removed MCP hosting attempt used the correct general `Generic Host` direction for the pinned SDK, but the tool container class lacked `[McpToolType]`. The installed SDK documented that `WithToolsFromAssembly(...)` discovers types marked with that attribute. As a consequence, the build could succeed while no MCP tools were registered.

The removed executable also wrote diagnostic text, including the bearer token and SQL connection string, to standard output. MCP stdio requires stdout to contain only protocol messages. This remains part of the historical failure record; the replacement architecture uses remote Streamable HTTP and must never log secrets.

## Historical security and functional gaps

- `settings.json` uses names that do not match the deserialized settings properties, so its SQL connection string currently resolves to an empty value.
- The static bearer-token setting is not checked against a request, does not authorize a caller, and is not a substitute for OAuth. In particular, stdio has no HTTP bearer header to validate.
- `execute_dql_query` accepts a free-form `WHERE` SQL fragment. This invalidates the documentation claim that all values are always parameterized.
- The blacklist validator allows known unsafe constructs such as `SELECT ... INTO` and `WAITFOR DELAY`; bracket escaping for table and JSON field names is also incomplete.
- Query results are unbounded and returned as an ad-hoc text string. There is no pagination, row limit, query timeout policy, result DTO/schema, allowlist, or audit trail.
- The database connector does not model schemas robustly and assumes SQL Server only. It has no end-to-end test against a real SQL Server.
- Test coverage is limited to basic validator and settings-manager cases. It does not exercise the known bypasses, actual settings template, MCP initialization/discovery, tool calls, or a database integration path.

## Product direction to preserve

The complete product direction is now defined by [AI Data Gateway — Project Handoff](./AI_DATA_GATEWAY_HANDOFF.md). It supersedes the earlier assumption that OData and a JSON query API would simply be added after an MCP SQL connector.

The target is a new self-hosted .NET 10 / ASP.NET Core gateway with:

- MCP over Streamable HTTP;
- a base read-only OData 4.01 profile in v1, including target Power BI scenarios;
- a versioned JSON query API and HTTP `QUERY`/`POST`;
- one Canonical Query Model shared by every protocol;
- real standalone OAuth through OpenIddict;
- a SQL Server provider that compiles structural queries into one parameterized `SELECT`;
- a SQLite control store for OAuth, catalog revisions, approval tokens, and minimal administration.

There is no caller-supplied SQL and no static bearer-token shortcut in the target architecture.

## Project analysis incorporated

The project owner's complete ChatGPT analysis was recovered and imported unchanged on 2026-07-18 as `docs/AI_DATA_GATEWAY_HANDOFF.md`. It is the implementation baseline. README, architecture, roadmap, and backlog have been reconciled around it.

The legacy code now exists only in Git history and the historical tag. This repository will be reused for the clean implementation under Apache License 2.0. Milestone 0 must still settle final product/namespace naming, package research, and the new solution/CI baseline before production implementation begins.
