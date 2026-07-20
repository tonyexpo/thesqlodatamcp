# ADR 0004 — Disposable SQL Server test infrastructure

- **Status:** Proposed
- **Date:** 2026-07-19

## Context

The SQL Server provider requires integration tests against a real disposable database locally and in CI. An API spike compiles with `Testcontainers.MsSql` 4.8.1 and `Microsoft.Data.SqlClient` 6.1.1 and pins `mcr.microsoft.com/mssql/server:2022-CU25-ubuntu-22.04` rather than a moving image tag.

The candidate now includes a provider-neutral fixture contract and a SQL Server implementation that recreates `TheSqlODataMcp_TestCatalog`, seeds 8,128 deterministic rows, verifies representative schema/data complexity, and drops that exact database in cleanup. The harness can either start the pinned Testcontainers image or use an administrator-supplied external SQL Server connection string redirected to `master`; it never logs that string.

The current environment still denies access to `/var/run/docker.sock`, and no external SQL Server connection is configured. Restore, warning-free build, and three non-Docker contract/parser tests pass, but build and static-test success alone are insufficient to accept the infrastructure choice.

## Proposed decision

Use `Testcontainers.MsSql` with an explicitly pinned Microsoft SQL Server image on supported local and CI environments. Reuse the same fixed fixture scripts against an explicitly configured external SQL Server when a caller already owns the container or server lifecycle.

## Acceptance gate

Change this ADR to Accepted only after:

1. the spike starts the pinned SQL Server image;
2. the deterministic fixture bootstraps with its exact schema and row-count contract;
3. `Microsoft.Data.SqlClient` opens a real connection and the metadata/data assertions pass;
4. an explicitly typed, parameterized `SELECT` succeeds;
5. teardown leaves the fixed fixture database absent;
6. the same owned-container test path is demonstrated on the intended CI runner.

No mock or build-only result can satisfy this gate.
