# ADR 0004 — Disposable SQL Server test infrastructure

- **Status:** Proposed
- **Date:** 2026-07-19

## Context

The SQL Server provider requires integration tests against a real disposable database locally and in CI. An API spike compiles with `Testcontainers.MsSql` 4.8.1 and `Microsoft.Data.SqlClient` 6.1.1 and pins `mcr.microsoft.com/mssql/server:2022-CU25-ubuntu-22.04` rather than a moving image tag.

The current environment denies access to `/var/run/docker.sock`. The test therefore fails during Testcontainers' Docker availability check, before the image or container starts. Build success alone is insufficient to accept the infrastructure choice.

## Proposed decision

Use `Testcontainers.MsSql` with an explicitly pinned Microsoft SQL Server image and run Docker-tagged integration tests on supported local and CI environments.

## Acceptance gate

Change this ADR to Accepted only after:

1. the spike starts the pinned SQL Server image;
2. `Microsoft.Data.SqlClient` opens a real connection;
3. an explicitly typed, parameterized `SELECT` succeeds;
4. the same test path is demonstrated on the intended CI runner;
5. failure and cleanup behavior are documented.

No mock or build-only result can satisfy this gate.
