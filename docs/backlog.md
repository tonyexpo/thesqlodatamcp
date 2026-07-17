# Backlog

## Future Features & Architecture Tasks

- [x] Initial project setup with .NET 10 target.
- [x] Create docs structure (changelog, architecture, backlog).
- [x] Define v1 MCP Tools: `list_tables`, `get_table_schema(table_name)`, `execute_dql_query(table_name, where_conditions_json_or_sql)`.
- [ ] Implement direct ADO.NET `SqlClient` integration for MS SQL Server.
- [ ] Implement simple Bearer token authentication with local settings file for v1.
- [ ] Develop T-SQL DQL Parser & Validator (Option A) to prevent SQL injection (reject subqueries, UNIONs, stored procedures).
- [x] Integrate `modelcontextprotocol` NuGet package for .NET 10.
- [ ] Build our own OAuth server (long-term goal).
- [ ] Support for multiple database types (PostgreSQL, MySQL) - *Future*.
- [ ] Connection string management and secure storage (e.g., environment variables or secret managers).
- [ ] Query result pagination and formatting (JSON, CSV).
- [ ] Audit logging of executed queries.
- [ ] Rate limiting and timeout enforcement for long-running queries.