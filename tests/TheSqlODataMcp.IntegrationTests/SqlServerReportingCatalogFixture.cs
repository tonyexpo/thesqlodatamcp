using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace TheSqlODataMcp.IntegrationTests;

internal sealed class SqlServerReportingCatalogFixture : IAsyncDisposable
{
    internal const string DatabaseName = "TheSqlODataMcp_TestCatalog";
    private const string ConnectionStringEnvironmentVariable = "THESQLODATAMCP_TEST_SQLSERVER_CONNECTION_STRING";
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-CU25-ubuntu-22.04";
    private readonly string masterConnectionString;
    private readonly MsSqlContainer? ownedContainer;

    private SqlServerReportingCatalogFixture(string masterConnectionString, MsSqlContainer? ownedContainer)
    {
        this.masterConnectionString = masterConnectionString;
        this.ownedContainer = ownedContainer;
    }

    internal string CatalogConnectionString => new SqlConnectionStringBuilder(masterConnectionString)
    {
        InitialCatalog = DatabaseName,
    }.ConnectionString;

    internal static async Task<SqlServerReportingCatalogFixture> CreateAsync(CancellationToken cancellationToken)
    {
        var configuredConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return new SqlServerReportingCatalogFixture(ToMasterConnectionString(configuredConnectionString), ownedContainer: null);
        }

        var container = new MsSqlBuilder()
            .WithImage(SqlServerImage)
            .WithPassword(CreateContainerPassword())
            .Build();
        await container.StartAsync(cancellationToken);
        return new SqlServerReportingCatalogFixture(ToMasterConnectionString(container.GetConnectionString()), container);
    }

    internal Task BootstrapAsync(CancellationToken cancellationToken) =>
        ExecuteScriptAsync(SqlServerFixtureAssets.BootstrapPath, cancellationToken);

    internal Task TeardownAsync(CancellationToken cancellationToken) =>
        ExecuteScriptAsync(SqlServerFixtureAssets.TeardownPath, cancellationToken);

    internal async Task<bool> DatabaseExistsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN DB_ID(@databaseName) IS NULL THEN 0 ELSE 1 END;";
        command.Parameters.Add(new SqlParameter("@databaseName", SqlDbType.NVarChar, 128) { Value = DatabaseName });
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    public async ValueTask DisposeAsync()
    {
        if (ownedContainer is not null)
        {
            await ownedContainer.DisposeAsync();
        }
    }

    private static string ToMasterConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master",
        };
        return builder.ConnectionString;
    }

    private static string CreateContainerPassword() =>
        $"Tsql!aA1_{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";

    private async Task ExecuteScriptAsync(string scriptPath, CancellationToken cancellationToken)
    {
        var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var batch in SqlServerFixtureGoSplitter.Split(script))
        {
            for (var repeat = 0; repeat < batch.RepeatCount; repeat++)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = batch.Sql;
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }
}

internal static class SqlServerFixtureAssets
{
    private const string FixtureDirectory = "fixtures/reporting-catalog/sqlserver";

    internal static string BootstrapPath => GetPath("bootstrap.sql");

    internal static string TeardownPath => GetPath("teardown.sql");

    private static string GetPath(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, FixtureDirectory, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The reporting catalog fixture asset was not copied to the test output.", path);
        }

        return path;
    }
}

internal sealed record SqlServerFixtureBatch(string Sql, int RepeatCount);

internal static class SqlServerFixtureGoSplitter
{
    internal static IReadOnlyList<SqlServerFixtureBatch> Split(string script)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        var batches = new List<SqlServerFixtureBatch>();
        var sql = new StringBuilder();
        using var reader = new StringReader(script);
        while (reader.ReadLine() is { } line)
        {
            if (TryParseGo(line, out var repeatCount))
            {
                AddBatch(batches, sql, repeatCount);
            }
            else
            {
                sql.AppendLine(line);
            }
        }

        AddBatch(batches, sql, repeatCount: 1);
        return batches;
    }

    private static void AddBatch(List<SqlServerFixtureBatch> batches, StringBuilder sql, int repeatCount)
    {
        var batchSql = sql.ToString().Trim();
        if (batchSql.Length > 0)
        {
            batches.Add(new SqlServerFixtureBatch(batchSql, repeatCount));
        }

        sql.Clear();
    }

    private static bool TryParseGo(string line, out int repeatCount)
    {
        var tokens = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0 || !string.Equals(tokens[0], "GO", StringComparison.OrdinalIgnoreCase))
        {
            repeatCount = 0;
            return false;
        }

        if (tokens.Length == 1)
        {
            repeatCount = 1;
            return true;
        }

        if (tokens.Length == 2 && int.TryParse(tokens[1], out repeatCount) && repeatCount > 0)
        {
            return true;
        }

        throw new FormatException($"Invalid SQL Server batch separator: '{line}'.");
    }
}
