using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace TheSqlODataMcp.Spikes.SqlServerTests;

public sealed class ReportingCatalogFixtureTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task BootstrapCreatesAndTeardownRemovesTheDeterministicReportingCatalog()
    {
        await using var fixture = await ReportingCatalogSqlServer.CreateAsync();

        try
        {
            await fixture.BootstrapAsync();

            await using (var catalog = await fixture.OpenCatalogConnectionAsync())
            {
                Assert.Equal(256, await ScalarIntAsync(catalog, "SELECT COUNT_BIG(*) FROM crm.Customers;"));
                Assert.Equal(512, await ScalarIntAsync(catalog, "SELECT COUNT_BIG(*) FROM crm.CustomerAddresses;"));
                Assert.Equal(12, await ScalarIntAsync(catalog, "SELECT COUNT_BIG(*) FROM inventory.Categories;"));
                Assert.Equal(128, await ScalarIntAsync(catalog, "SELECT COUNT_BIG(*) FROM inventory.Products;"));
                Assert.Equal(4, await ScalarIntAsync(catalog, "SELECT COUNT_BIG(*) FROM inventory.Warehouses;"));
                Assert.Equal(512, await ScalarIntAsync(catalog, "SELECT COUNT_BIG(*) FROM inventory.StockBalances;"));
                Assert.Equal(1024, await ScalarIntAsync(catalog, "SELECT COUNT_BIG(*) FROM sales.Invoices;"));
                Assert.Equal(4096, await ScalarIntAsync(catalog, "SELECT COUNT_BIG(*) FROM sales.InvoiceLines;"));
                Assert.Equal(512, await ScalarIntAsync(catalog, "SELECT COUNT_BIG(*) FROM sales.Payments;"));
                Assert.Equal(1024, await ScalarIntAsync(catalog, "SELECT COUNT_BIG(*) FROM sales.InvoiceStatuses;"));
                Assert.Equal(16, await ScalarIntAsync(catalog, "SELECT COUNT_BIG(*) FROM operations.TypeCoverage;"));
                Assert.Equal(32, await ScalarIntAsync(catalog, "SELECT COUNT_BIG(*) FROM archive.Invoices;"));

                Assert.Equal(7, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.schemas WHERE name IN ('crm', 'sales', 'inventory', 'reporting', 'operations', 'archive', 'unsupported');"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.identity_columns WHERE object_id = OBJECT_ID(N'crm.Customers') AND name = N'CustomerId';"));
                Assert.Equal(2, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.index_columns AS ic JOIN sys.indexes AS i ON i.object_id = ic.object_id AND i.index_id = ic.index_id WHERE i.object_id = OBJECT_ID(N'inventory.StockBalances') AND i.name = N'PK_StockBalances' AND i.is_primary_key = 1;"));
                Assert.Equal(2, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'sales.Invoices') AND referenced_object_id = OBJECT_ID(N'crm.Customers');"));
                Assert.Equal(2, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'sales.Invoices') AND name IN ('FK_Invoices_BillToAddress', 'FK_Invoices_ShipToAddress');"));
                Assert.Equal(4, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.foreign_key_columns AS f JOIN sys.foreign_keys AS k ON k.object_id = f.constraint_object_id WHERE k.parent_object_id = OBJECT_ID(N'sales.Invoices') AND k.name IN ('FK_Invoices_BillToAddress', 'FK_Invoices_ShipToAddress');"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'crm.Customers') AND referenced_object_id = OBJECT_ID(N'crm.Customers');"));
                Assert.Equal(0, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.foreign_key_columns AS f JOIN sys.columns AS c ON c.object_id = f.parent_object_id AND c.column_id = f.parent_column_id WHERE c.object_id = OBJECT_ID(N'sales.Invoices') AND c.name = N'LegacyCustomerCode';"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'crm.Customers') AND name = N'UQ_Customers_CustomerCode' AND is_unique = 1;"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'crm.Customers') AND name = N'UX_Customers_Email_WhenPresent' AND has_filter = 1;"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.computed_columns WHERE object_id = OBJECT_ID(N'sales.Invoices') AND name = N'TotalDue' AND is_persisted = 1;"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'operations.TypeCoverage') AND name = N'VersionValue' AND system_type_id = 189;"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.columns AS c JOIN sys.types AS t ON t.user_type_id = c.user_type_id WHERE c.object_id = OBJECT_ID(N'operations.TypeCoverage') AND c.name = N'HierarchyValue' AND t.name = N'hierarchyid';"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'operations.TypeCoverage') AND name = N'VariantValue' AND system_type_id = 98;"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'sales.InvoiceStatuses') AND temporal_type = 2;"));
                Assert.Equal(2, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.views WHERE object_id IN (OBJECT_ID(N'reporting.InvoiceDetail'), OBJECT_ID(N'reporting.InvoiceMonthlySummary'));"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.extended_properties WHERE name = N'MS_Description' AND major_id = OBJECT_ID(N'sales.Invoices') AND minor_id = 0;"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.extended_properties WHERE name = N'MS_Description' AND major_id = OBJECT_ID(N'sales.Invoices') AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'sales.Invoices'), N'TotalDue', 'ColumnId');"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.extended_properties WHERE name = N'MS_Description' AND major_id = OBJECT_ID(N'reporting.InvoiceDetail') AND minor_id = 0;"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.procedures WHERE object_id = OBJECT_ID(N'unsupported.RebuildFixtureCache');"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID(N'unsupported.FixtureScalar') AND type = 'FN';"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.sequences WHERE object_id = OBJECT_ID(N'unsupported.FixtureSequence');"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.synonyms WHERE name = N'CustomerAlias' AND schema_id = SCHEMA_ID(N'unsupported');"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.table_types WHERE name = N'FixtureTableType';"));
                Assert.Equal(1, await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sys.triggers WHERE object_id = OBJECT_ID(N'crm.UnsupportedNoopCustomerTrigger');"));
                Assert.True(await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM crm.Customers WHERE CountryCode IS NULL;") > 0);
                Assert.True(await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM crm.Customers WHERE CountryCode IS NOT NULL;") > 0);
                Assert.True(await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sales.Invoices WHERE CurrencyCode IS NULL;") > 0);
                Assert.True(await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sales.Invoices WHERE CurrencyCode IS NOT NULL;") > 0);
                Assert.Equal(4, await ScalarIntAsync(catalog, "SELECT COUNT(DISTINCT StatusCode) FROM sales.InvoiceStatuses;"));
                Assert.True(await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sales.Invoices WHERE DiscountAmount = 0;") > 0);
                Assert.True(await ScalarIntAsync(catalog, "SELECT COUNT(*) FROM sales.Invoices WHERE DiscountAmount > 0;") > 0);

                await using var parameterCommand = catalog.CreateCommand();
                parameterCommand.CommandText = "SELECT COUNT_BIG(*) FROM sales.Invoices WHERE InvoiceId = @invoiceId AND InvoiceNumber = @invoiceNumber;";
                parameterCommand.Parameters.Add(new SqlParameter("@invoiceId", SqlDbType.Int) { Value = 42 });
                parameterCommand.Parameters.Add(new SqlParameter("@invoiceNumber", SqlDbType.VarChar, 24) { Value = "INV-000042" });
                Assert.Equal(1, Convert.ToInt32(await parameterCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
            }

            await fixture.TeardownAsync();
            Assert.False(await fixture.DatabaseExistsAsync());
        }
        finally
        {
            await fixture.TeardownAsync();
        }
    }

    private static async Task<int> ScalarIntAsync(SqlConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }
}

internal sealed class ReportingCatalogSqlServer : IAsyncDisposable
{
    internal const string DatabaseName = "TheSqlODataMcp_TestCatalog";
    private const string ConnectionStringEnvironmentVariable = "THESQLODATAMCP_TEST_SQLSERVER_CONNECTION_STRING";
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-CU25-ubuntu-22.04";
    private readonly string _masterConnectionString;
    private readonly MsSqlContainer? _ownedContainer;

    private ReportingCatalogSqlServer(string masterConnectionString, MsSqlContainer? ownedContainer)
    {
        _masterConnectionString = masterConnectionString;
        _ownedContainer = ownedContainer;
    }

    public static async Task<ReportingCatalogSqlServer> CreateAsync()
    {
        var configuredConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return new ReportingCatalogSqlServer(ToMasterConnectionString(configuredConnectionString), ownedContainer: null);
        }

        var container = new MsSqlBuilder()
            .WithImage(SqlServerImage)
            .WithPassword(CreateContainerPassword())
            .Build();
        await container.StartAsync(CancellationToken.None);
        return new ReportingCatalogSqlServer(ToMasterConnectionString(container.GetConnectionString()), container);
    }

    public async Task BootstrapAsync()
    {
        await ExecuteScriptAsync(FixtureAssets.BootstrapPath);
    }

    public async Task TeardownAsync()
    {
        await ExecuteScriptAsync(FixtureAssets.TeardownPath);
    }

    public async Task<SqlConnection> OpenCatalogConnectionAsync()
    {
        var connection = new SqlConnection(new SqlConnectionStringBuilder(_masterConnectionString)
        {
            InitialCatalog = DatabaseName,
        }.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<bool> DatabaseExistsAsync()
    {
        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN DB_ID(@databaseName) IS NULL THEN 0 ELSE 1 END;";
        command.Parameters.Add(new SqlParameter("@databaseName", SqlDbType.NVarChar, 128) { Value = DatabaseName });
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1;
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownedContainer is not null)
        {
            await _ownedContainer.DisposeAsync();
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

    private static string CreateContainerPassword()
    {
        return $"Tsql!aA1_{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";
    }

    private async Task ExecuteScriptAsync(string scriptPath)
    {
        var script = await File.ReadAllTextAsync(scriptPath);
        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        foreach (var batch in SqlServerGoSplitter.Split(script))
        {
            for (var repeat = 0; repeat < batch.RepeatCount; repeat++)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = batch.Sql;
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}

internal static class FixtureAssets
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

internal sealed record SqlServerBatch(string Sql, int RepeatCount);

internal static class SqlServerGoSplitter
{
    internal static IReadOnlyList<SqlServerBatch> Split(string script)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        var batches = new List<SqlServerBatch>();
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

    private static void AddBatch(List<SqlServerBatch> batches, StringBuilder sql, int repeatCount)
    {
        var batchSql = sql.ToString().Trim();
        if (batchSql.Length > 0)
        {
            batches.Add(new SqlServerBatch(batchSql, repeatCount));
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
