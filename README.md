# SQL OData MCP Connector

## Project Intent

This project aims to provide a Model Context Protocol (MCP) connector that acts as a bridge to execute SQL queries (strictly Data Query Language - DQL, i.e., `SELECT` statements only). The goal is to make database access securely available to various AI agents and harnesses, whether deployed on-premises or in SaaS environments.

By leveraging the MCP standard, this connector enables seamless integration between language models and relational databases, ensuring that interactions are restricted to read-only operations (DQL) to prevent any data modification or destructive actions.

## Features

- **Read-Only Access:** Strictly enforces DQL (SELECT queries) to ensure data safety.
- **MCP Standard Compliance:** Implements the Model Context Protocol for standardized agent integration.
- **Flexible Deployment:** Supports both on-premises and SaaS harness environments.
- **Secure Agent Integration:** Provides a controlled, audit-friendly bridge between AI agents and underlying SQL databases.

## MCP Tools

The connector exposes the following MCP tools:

- `list_tables`: List available tables in the database.
- `get_table_schema(table_name)`: Retrieve the schema (column names and data types) for a specific table.
- `execute_dql_query(table_name, where_conditions_json_or_sql)`: Execute a validated DQL query using parameterized conditions.

## Documentation

- [Project Status & Handoff](./docs/project-status-handoff.md) — current verified status, agent transition, and prerequisites for restarting development.
- [Architecture](./docs/architecture.md)
- [Changelog](./docs/changelog.md)
- [Backlog](./docs/backlog.md)
