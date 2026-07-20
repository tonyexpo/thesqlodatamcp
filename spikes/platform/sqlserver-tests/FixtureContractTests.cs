using System.Text.Json;
using Xunit;

namespace TheSqlODataMcp.Spikes.SqlServerTests;

public sealed class FixtureContractTests
{
    [Fact]
    [Trait("Category", "FixtureStatic")]
    public void ContractDeclaresTheFixedCatalogAndRequiredRowCounts()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures/reporting-catalog/contract.json")));
        var root = document.RootElement;
        Assert.Equal("1.0", root.GetProperty("contractVersion").GetString());
        Assert.Equal(ReportingCatalogSqlServer.DatabaseName, root.GetProperty("databaseName").GetString());
        Assert.Equal(7, root.GetProperty("schemas").GetArrayLength());
        var views = root.GetProperty("logicalViews");
        Assert.Equal(2, views.GetArrayLength());
        Assert.Contains("reporting.InvoiceDetail", views.EnumerateArray().Select(element => element.GetString()));
        Assert.Contains("reporting.InvoiceMonthlySummary", views.EnumerateArray().Select(element => element.GetString()));
        var relationships = root.GetProperty("portableRelationships");
        Assert.Equal("crm.Customers.CustomerId", relationships.GetProperty("simplePrimaryKey").GetString());
        Assert.Equal("inventory.StockBalances(ProductId,WarehouseId)", relationships.GetProperty("compositePrimaryKey").GetString());
        Assert.Equal("sales.Invoices(BillToCustomerId,BillToAddressKind)->crm.CustomerAddresses(CustomerId,AddressKind)", relationships.GetProperty("compositeForeignKey").GetString());
        Assert.Equal(2, relationships.GetProperty("ambiguousCustomerForeignKeys").GetArrayLength());
        Assert.Equal("sales.Invoices.LegacyCustomerCode", relationships.GetProperty("legacyCodeCompatibleWithoutForeignKey").GetString());
        Assert.Equal("crm.Customers.ParentCustomerId", relationships.GetProperty("selfHierarchy").GetString());
        Assert.Contains("generatedIdentity", root.GetProperty("portableFeatures").EnumerateArray().Select(element => element.GetString()));
        Assert.Contains("uniqueConstraint", root.GetProperty("portableFeatures").EnumerateArray().Select(element => element.GetString()));
        Assert.Contains("computedColumn", root.GetProperty("portableFeatures").EnumerateArray().Select(element => element.GetString()));
        Assert.Contains("keylessAggregateView", root.GetProperty("portableFeatures").EnumerateArray().Select(element => element.GetString()));
        Assert.Contains("filteredUniqueIndex", root.GetProperty("sqlServerExtensions").EnumerateArray().Select(element => element.GetString()));
        Assert.Contains("persistedComputedColumn", root.GetProperty("sqlServerExtensions").EnumerateArray().Select(element => element.GetString()));
        Assert.Contains("systemVersionedTemporalTable", root.GetProperty("sqlServerExtensions").EnumerateArray().Select(element => element.GetString()));
        Assert.Contains("rowversion", root.GetProperty("sqlServerExtensions").EnumerateArray().Select(element => element.GetString()));
        Assert.Contains("hierarchyid", root.GetProperty("sqlServerExtensions").EnumerateArray().Select(element => element.GetString()));
        Assert.Contains("sql_variant", root.GetProperty("sqlServerExtensions").EnumerateArray().Select(element => element.GetString()));

        var rowCounts = root.GetProperty("rowCounts");
        Assert.Equal(256, rowCounts.GetProperty("crm.Customers").GetInt32());
        Assert.Equal(512, rowCounts.GetProperty("crm.CustomerAddresses").GetInt32());
        Assert.Equal(12, rowCounts.GetProperty("inventory.Categories").GetInt32());
        Assert.Equal(128, rowCounts.GetProperty("inventory.Products").GetInt32());
        Assert.Equal(4, rowCounts.GetProperty("inventory.Warehouses").GetInt32());
        Assert.Equal(512, rowCounts.GetProperty("inventory.StockBalances").GetInt32());
        Assert.Equal(1024, rowCounts.GetProperty("sales.Invoices").GetInt32());
        Assert.Equal(4096, rowCounts.GetProperty("sales.InvoiceLines").GetInt32());
        Assert.Equal(512, rowCounts.GetProperty("sales.Payments").GetInt32());
        Assert.Equal(1024, rowCounts.GetProperty("sales.InvoiceStatuses").GetInt32());
        Assert.Equal(16, rowCounts.GetProperty("operations.TypeCoverage").GetInt32());
        Assert.Equal(32, rowCounts.GetProperty("archive.Invoices").GetInt32());
    }

    [Fact]
    [Trait("Category", "FixtureStatic")]
    public void GoSplitterPreservesBatchesAndSupportsRepeatCounts()
    {
        var batches = SqlServerGoSplitter.Split("SELECT 1;\n GO \nSELECT 2;\n\tGO\t2\n");
        Assert.Collection(
            batches,
            first =>
            {
                Assert.Equal("SELECT 1;", first.Sql);
                Assert.Equal(1, first.RepeatCount);
            },
            second =>
            {
                Assert.Equal("SELECT 2;", second.Sql);
                Assert.Equal(2, second.RepeatCount);
            });

        Assert.Throws<FormatException>(() => SqlServerGoSplitter.Split("SELECT 1;\nGO 0\n"));
        Assert.Throws<FormatException>(() => SqlServerGoSplitter.Split("SELECT 1;\nGO twice\n"));
    }

    [Fact]
    [Trait("Category", "FixtureStatic")]
    public void BootstrapUsesStaticCatalogNameAndDeterministicSeedPrimitives()
    {
        var bootstrap = File.ReadAllText(FixtureAssets.BootstrapPath);
        Assert.Contains("CREATE DATABASE [TheSqlODataMcp_TestCatalog]", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("GETDATE", bootstrap, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SYSDATETIME", bootstrap, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NEWID", bootstrap, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RAND", bootstrap, StringComparison.OrdinalIgnoreCase);
    }
}
