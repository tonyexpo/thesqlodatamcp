# ADR 0004 — Disposable SQL Server test infrastructure

- **Status:** Accepted
- **Date:** 2026-07-21

## Context

The SQL Server provider requires integration tests against a real disposable database locally and in CI. An API spike compiles with `Testcontainers.MsSql` 4.8.1 and `Microsoft.Data.SqlClient` 6.1.1 and pins `mcr.microsoft.com/mssql/server:2022-CU25-ubuntu-22.04` rather than a moving image tag.

The candidate now includes a provider-neutral fixture contract and a SQL Server implementation that recreates `TheSqlODataMcp_TestCatalog`, seeds 8,128 deterministic rows, verifies representative schema/data complexity, and drops that exact database in cleanup. The harness can either start the pinned Testcontainers image or use an administrator-supplied external SQL Server connection string redirected to `master`; it never logs that string.

The local agent environment still denies access to `/var/run/docker.sock`, and no external SQL Server connection is configured. This is a local environment limitation rather than a product or CI limitation.

## Decision

Use `Testcontainers.MsSql` with an explicitly pinned Microsoft SQL Server image on supported local and CI environments. Reuse the same fixed fixture scripts against an explicitly configured external SQL Server when a caller already owns the container or server lifecycle.

## Acceptance evidence

GitHub Actions run [29778536859](https://github.com/tonyexpo/thesqlodatamcp/actions/runs/29778536859) completed successfully on the intended Ubuntu runner for commit `54a31dd` on 2026-07-20. Its dependent `sqlserver-integration` job:

1. confirmed Docker availability;
2. started the pinned SQL Server image through the owned-Testcontainers path;
3. bootstrapped the deterministic fixture and passed its schema and row-count assertions;
4. opened a real `Microsoft.Data.SqlClient` connection and executed the explicitly typed, parameterized `SELECT`;
5. ran teardown and proved that the fixed fixture database was absent;
6. passed the complete static and real-integration test project after the dependent production `validate` job succeeded.

## Consequences

- The pinned Testcontainers path is the default disposable SQL Server integration-test infrastructure.
- The external-server mode remains supported for owner-managed local infrastructure and uses the same fixture and assertions.
- Lack of Docker access in an individual agent sandbox does not invalidate the accepted infrastructure when the intended CI runner remains green.
- Future image, Testcontainers, or `Microsoft.Data.SqlClient` upgrades must rerun the real integration gate before acceptance.
