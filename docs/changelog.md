# Changelog

## [Unreleased]

### Added
- Initial project setup.
- MCP connector concept for SQL DQL queries.

## [v0.5.0 - Phase 3, 4 & 5 Completion: SqlClient Connector, MCP Tools Execution & Server Initialization Structure]

### Database Connector & MCP Tools
- Completed Phase 3: Implemented `DatabaseConnector` with `ListTables()` and `GetTableSchema(tableName)` using `SqlConnection` and `SqlCommand`.
- Completed Phase 4: Implemented `McpTools.ExecuteDqlQueryAsync` to execute validated DQL queries via `SqlClient` with parameterized conditions.
- JSON condition processing is now supported in `execute_dql_query`, converting JSON filters to parameterized SQL WHERE clauses.
- Security enforcement ensures all values are passed as pure parameters via `SqlCommand.Parameters`.

### MCP Server & Authentication
- Completed Phase 5 structure: Bearer token authentication validation implemented using the token from `settings.json`.
- MCP server initialization structure prepared using `ModelContextProtocol.Server`, with tools registration placeholders (`list_tables`, `get_table_schema`, `execute_dql_query`) pending full SDK API alignment for transport initialization (stdio).

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