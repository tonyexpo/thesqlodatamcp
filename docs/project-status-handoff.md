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

The repository therefore remains an **incomplete proof of concept**, not a safe SQL connector and not a release candidate. No production database credentials should be placed in its current `settings.json` convention or used with the current executable.

## Verified current state

The latest source at the time of this handoff builds successfully and the existing unit suite reports 13 passing tests. That verification is deliberately narrow: it does **not** prove MCP protocol interoperability, tool registration, SQL connectivity, OAuth/authentication, or read-only security.

The current MCP hosting attempt uses the correct general `Generic Host` direction for the pinned SDK, but the tool container class lacks `[McpToolType]`. The installed SDK documents that `WithToolsFromAssembly(...)` discovers types marked with that attribute. As a consequence, the build can succeed while no MCP tools are registered.

The executable also currently writes diagnostic text, including the bearer token and SQL connection string, to standard output. MCP stdio requires stdout to contain only protocol messages, so this must be corrected before any interoperability test. Secrets must never be logged.

## Security and functional gaps

- `settings.json` uses names that do not match the deserialized settings properties, so its SQL connection string currently resolves to an empty value.
- The static bearer-token setting is not checked against a request, does not authorize a caller, and is not a substitute for OAuth. In particular, stdio has no HTTP bearer header to validate.
- `execute_dql_query` accepts a free-form `WHERE` SQL fragment. This invalidates the documentation claim that all values are always parameterized.
- The blacklist validator allows known unsafe constructs such as `SELECT ... INTO` and `WAITFOR DELAY`; bracket escaping for table and JSON field names is also incomplete.
- Query results are unbounded and returned as an ad-hoc text string. There is no pagination, row limit, query timeout policy, result DTO/schema, allowlist, or audit trail.
- The database connector does not model schemas robustly and assumes SQL Server only. It has no end-to-end test against a real SQL Server.
- Test coverage is limited to basic validator and settings-manager cases. It does not exercise the known bypasses, actual settings template, MCP initialization/discovery, tool calls, or a database integration path.

## Product direction to preserve

The intended product direction is valid:

1. a secure, read-only SQL capability for MCP clients;
2. a later OData-facing adapter for older clients, including Power BI;
3. a later ATP JSON-query capability.

These interfaces should not be built independently on top of arbitrary SQL text. The design should first define a common, restricted query contract: authorized entities or views, selectable fields, typed filters, ordering, pagination, result limits, and a stable result schema. MCP, OData, and ATP JSON can then be adapters over that contract.

## Deferred project analysis

The project owner has a prior analysis document on another computer. When it becomes available, add it to `docs/` and reconcile it with the backlog before implementation resumes. It is expected to contain product and architecture decisions that must guide the remaining work.

Until that analysis is incorporated, the current code should be treated as reference material only. Do not expand the feature set or claim a production security posture.
