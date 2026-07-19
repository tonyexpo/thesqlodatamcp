# Disposable SQL Server integration-test spike

## Selection and result

Pinned packages: `Testcontainers.MsSql` **4.8.1** and `Microsoft.Data.SqlClient` **6.1.1**.

The test pins the Microsoft Container Registry image `mcr.microsoft.com/mssql/server:2022-CU25-ubuntu-22.04`, corresponding to SQL Server 2022 CU 25 (build 16.0.4262.2, July 2026). It intentionally does not use a moving `latest` tag. The registry manifest was verified on 2026-07-19 with digest `sha256:e07b9699a2b749969f19d86563ceeea22bd3a69f7f1db85a8d1ac4bdaf0c6f56`; the container itself was not started because Docker's socket is unavailable.

An authorized test run was attempted on 2026-07-19 and failed in Testcontainers' Docker availability check with `permission denied` for `/var/run/docker.sock`. The project restores and builds with zero warnings or errors, but ADR 0004 remains Proposed until the real container test passes locally and in CI.

The test uses `MsSqlBuilder` to provision a real Microsoft SQL Server container, opens a `SqlConnection`, and executes a parameterized `SELECT` with an explicitly typed `SqlParameter` (`SqlDbType.Int`). It is deliberately a real integration test, tagged `DockerIntegration`; no mocked SQL Server is substituted.

## Commands

```bash
dotnet restore spikes/platform/sqlserver-tests/SqlServerTests.ApiSpike.csproj
dotnet test spikes/platform/sqlserver-tests/SqlServerTests.ApiSpike.csproj --no-restore
```

Docker is required for the second command. On a CI runner, enable Docker (or its supported daemon equivalent) and do not filter out `DockerIntegration`. If Docker is unavailable, restore/build remains valid but the integration run is correctly unavailable rather than skipped or simulated.

## Primary sources

- https://dotnet.testcontainers.org/modules/mssql/
- https://github.com/testcontainers/testcontainers-dotnet
- https://learn.microsoft.com/sql/linux/quickstart-install-connect-docker
- https://learn.microsoft.com/sql/linux/sql-server-linux-release-notes-2022
- https://learn.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection
