# Backlog - SQL OData MCP Connector

## Development Phases v1

### Phase 1: Configuration & Settings Management
- [x] Implement reading and parsing of `settings.json` (Bearer token, SQL connection string).
- [x] Ensure settings are loaded securely at application startup.

### Phase 2: T-SQL DQL Parser & Validator (Option A - Security)
- [ ] Create a parser/validator that strictly enforces DQL (`SELECT` statements only).
- [ ] Reject DML, DDL, subqueries, `UNION`s, stored procedure calls, and other dangerous constructs.
- [ ] Ensure values are passed as pure parameters via `SqlCommand.Parameters`.

### Phase 3: Database Connector Integration (SqlClient)
- [ ] Implement connection using `SqlConnection` and `SqlCommand`.
- [ ] Implement the `list_tables` tool: Query system views (e.g., `sys.tables`) to list available tables.
- [ ] Implement the `get_table_schema(table_name)` tool: Query system views (e.g., `sys.columns`, `sys.types`) to get column names and types for a specific table.

### Phase 4: MCP Tools Implementation (`modelcontextprotocol` NuGet package)
- [ ] Register MCP tools: `list_tables`, `get_table_schema(table_name)`, `execute_dql_query(table_name, where_conditions_json_or_sql)`.
- [ ] Implement `execute_dql_query`: Use the T-SQL DQL Parser & Validator to validate the query or conditions, then execute using `SqlClient` with parameterized queries.

### Phase 5: MCP Server Initialization & Authentication
- [ ] Initialize the MCP server using `ModelContextProtocol` and `ModelContextProtocol.Server`.
- [ ] Implement Bearer token authentication validation using the token from `settings.json`.

### Phase 6: Testing & Validation
- [ ] Test the MCP server locally (e.g., via MCP inspector or compatible client).
- [ ] Verify security (SQL injection prevention, DQL only enforcement).
- [ ] Verify authentication (Bearer token validation).

## vNext / Future Improvements
- Full OAuth server implementation
- Advanced security features beyond the T-SQL DQL Parser
- Support for other database types (PostgreSQL, MySQL, etc.)
- Advanced query caching and performance optimization
- Integration with more MCP clients and frameworks
