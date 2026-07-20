# SQL Server reporting-catalog fixture spike

This project proves the deterministic fixture contract under
[`tests/fixtures/reporting-catalog`](../../../tests/fixtures/reporting-catalog/README.md).
The provider-neutral contract declares the fixed catalog name and row counts; only
the SQL Server DDL, `GO` batch execution, and Testcontainers harness live here.

The catalog is always `TheSqlODataMcp_TestCatalog`. Bootstrap connects to `master`,
drops that exact stale catalog if it exists, creates and seeds it, and teardown
force-disconnects and drops only that exact name. It never interpolates a database
name from configuration. Seeds are set-based and use fixed values; no time, random,
network, or external-file source is used.

## Modes

Without configuration, the integration test starts the pinned
`mcr.microsoft.com/mssql/server:2022-CU25-ubuntu-22.04` Testcontainers image using
`Testcontainers.MsSql` 4.8.1, `Microsoft.Data.SqlClient` 6.1.1, and a random
in-memory container password that is never logged. With
`THESQLODATAMCP_TEST_SQLSERVER_CONNECTION_STRING` set, it uses that existing SQL
Server instead. In either mode the supplied connection string is redirected to
`master`; it must have permission to create and drop the fixed fixture database.
The test never logs the connection string. Use a local secret/environment setting,
not a checked-in value, for example:

```bash
export THESQLODATAMCP_TEST_SQLSERVER_CONNECTION_STRING='Server=localhost,1433;User ID=sa;Password=<local-secret>;Encrypt=True;TrustServerCertificate=True'
```

## Commands

```bash
dotnet restore spikes/platform/sqlserver-tests/SqlServerTests.ApiSpike.csproj
dotnet build spikes/platform/sqlserver-tests/SqlServerTests.ApiSpike.csproj --no-restore
dotnet test spikes/platform/sqlserver-tests/SqlServerTests.ApiSpike.csproj --no-build --filter 'Category=FixtureStatic'
dotnet test spikes/platform/sqlserver-tests/SqlServerTests.ApiSpike.csproj --no-build --filter 'Category=SqlServerIntegration'
```

The static tests require no Docker or SQL Server. The integration test needs either
Docker access or the explicitly configured external server. It bootstraps before
assertions and tears down in `finally`; a failed process may still need this safe,
fixed-name cleanup run from a `master` connection:

```bash
sqlcmd -S <server> -U <user> -P '<local-secret>' -d master -i tests/fixtures/reporting-catalog/sqlserver/teardown.sql
```

The fixture covers identities, simple/composite keys and foreign keys, ambiguous
customer paths, a self hierarchy, nullable values, filtered unique indexes, checks,
defaults, persisted computed columns, rowversion, a temporal table, keyless detail
and aggregate views, descriptions, broad scalar types, and inert unsupported SQL
Server objects. It is infrastructure evidence only; it does not change the
production catalog boundary.
