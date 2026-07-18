# Architecture

> **Status — 2026-07-18:** This file describes the intended architecture, not the delivered security posture. The current implementation is an incomplete proof of concept. Before implementation resumes, read [Project Status & Handoff](./project-status-handoff.md) and follow the recovery-first [Backlog](./backlog.md). In particular, static bearer configuration is not OAuth, and a blacklist parser is not an adequate read-only security boundary.

The SQL OData MCP Connector is designed as a lightweight, secure bridge between AI agents (using the Model Context Protocol) and relational databases, specifically targeting MS SQL Server via direct ADO.NET `SqlClient`. 

Key components:
1. **MCP Server Layer**: Handles connections from MCP clients (agents/harnesses) via stdio or SSE protocols, exposing specific DQL tools using the `modelcontextprotocol` NuGet package. The v1 tools include:
   - `list_tables`: Returns the list of all accessible tables in the database.
   - `get_table_schema(table_name)`: Returns the schema (columns, data types, primary keys) of a specific table.
   - `execute_dql_query(table_name, where_conditions_json_or_sql)`: Executes the DQL query on the table with filters specified.
2. **Authentication Layer (v1)**: Simple Bearer token authentication. The token is provided directly and saved locally in a settings file for validation. (Note: A full OAuth server is planned for future iterations).
3. **Query Parser & Validator (SQL Injection Prevention - Option A)**: Ensures only safe DQL (`SELECT` statements) are executed. It parses T-SQL or structured filter inputs, rejecting any DML (INSERT, UPDATE, DELETE), DDL (CREATE, DROP), subqueries, UNIONs, or stored procedure calls. Values are extracted and passed as pure parameters via `SqlCommand.Parameters`.
4. **Database Connector**: Establishes the connection to the underlying MS SQL database using standard connection strings and ADO.NET (`SqlConnection`, `SqlCommand`, `SqlDataReader`).
