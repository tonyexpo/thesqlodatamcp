# Changelog

## [Unreleased]

### Added
- Initial project setup.
- MCP connector concept for SQL DQL queries.
- Public project handoff documenting the transition from Qwen 3.6 35B to Codex 5.6 Terra.
- A verified project-status document and a recovery-first backlog for the future MCP, OData, and ATP JSON work.

### Changed
- Corrected the interpretation of v0.6.0: the project compiles, but MCP tool discovery and end-to-end interoperability were not verified. The current `McpTools` class is not marked with the SDK-required `McpToolType` attribute.
- Reclassified the current implementation as an incomplete proof of concept, not a deployable read-only SQL connector.

### Known limitations
- Current stdout diagnostics are incompatible with MCP stdio and expose secrets.
- Current bearer-token configuration is not real client authentication or OAuth.
- The current DQL blacklist and free-form `WHERE` input do not constitute a safe read-only boundary.
- OData and ATP JSON support are future work only.

## [v0.6.0 - MCP Server Hosting Attempt]

### MCP Server & Tools Integration
- Added a compiling attempt to wire up the ModelContextProtocol server transport and tool registration in `Program.cs`.
- Used Microsoft.Extensions.Hosting's generic host pattern (`Host.CreateApplicationBuilder`) to initialize the application.
- Applied `[McpToolAttribute]` to three methods and used `AddMcpServer`, `WithStdioServerTransport`, and `WithToolsFromAssembly`.
- The project compiles successfully with zero errors, but the class-level `[McpToolType]` marker and a protocol-level verification were omitted; this version must not be described as completed MCP tool registration.

## [v0.5.1 - Phase 5 Structure & Authentication (Transport/Tools Placeholder)]

### MCP Server & Authentication
- Completed Phase 5 structure: Bearer token authentication validation implemented using the token from `settings.json`.
- MCP server initialization structure prepared using `ModelContextProtocol.Server`.
- Note: Transport initialization (stdio) and tool registration execution are left as placeholders pending full SDK API alignment or a compatible version of the `ModelContextProtocol` package, as the current version (0.1.0-preview.1.25171.12) does not expose `ModelContextProtocol.Protocol.Models` or `ModelContextProtocol.Transport.Stdio`.

## [v0.5.0 - Phase 3, 4 & 5 Completion: SqlClient Connector, MCP Tools Execution & Server Initialization Structure]

### Database Connector & MCP Tools
- Completed Phase 3: Implemented `DatabaseConnector` with `ListTables()` and `GetTableSchema(tableName)` using `SqlConnection` and `SqlCommand`.
- Completed Phase 4: Implemented `McpTools.ExecuteDqlQueryAsync` to execute validated DQL queries via `SqlClient` with parameterized conditions.
- JSON condition processing is now supported in `execute_dql_query`, converting JSON filters to parameterized SQL WHERE clauses.
- Security enforcement ensures all values are passed as pure parameters via `SqlCommand.Parameters`.

## [v0.2.0 - Architectural Decisions & v1 Scope]

### Platform & Stack
- Target framework updated to .NET 10 (`net10.0`).
- Database access restricted to direct ADO.NET `SqlClient` for MS SQL Server (no EF/ORM).
- MCP integration via the `modelcontextprotocol` NuGet package.

### Security & Auth
- Implemented strict DQL-only enforcement to prevent SQL injection and data modification (Option A: T-SQL DQL Parser & Validator).
- Authentication simplified to simple Bearer token authentication with a direct token saved locally in a settings file for v1.

### MCP Tools (v1)
- Exposed tools: `list_tables`, `get_table_schema(table_name)`, `execute_dql_query(table_name, where_conditions_json_or_sql)`.
