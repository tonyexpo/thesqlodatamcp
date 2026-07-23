using TheSqlODataMcp.Core.Catalog;
using TheSqlODataMcp.SqlServer;
using Xunit;

namespace TheSqlODataMcp.IntegrationTests;

public sealed class SqlServerCatalogIntrospectorIntegrationTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task DiscoversTheDeterministicTechnicalCatalogAndExcludesUnsupportedObjects()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        await using var fixture = await SqlServerReportingCatalogFixture.CreateAsync(timeout.Token);

        try
        {
            await fixture.BootstrapAsync(timeout.Token);
            var introspector = new SqlServerCatalogIntrospector(fixture.CatalogConnectionString);

            var first = await introspector.IntrospectAsync(timeout.Token);
            var second = await introspector.IntrospectAsync(timeout.Token);

            AssertCatalogShape(first);
            Assert.Equal(
                TechnicalCatalogCanonicalJson.Serialize(first),
                TechnicalCatalogCanonicalJson.Serialize(second));
            Assert.Equal(
                TechnicalCatalogCanonicalJson.ComputeStructuralHash(first),
                TechnicalCatalogCanonicalJson.ComputeStructuralHash(second));

            await fixture.TeardownAsync(timeout.Token);
            Assert.False(await fixture.DatabaseExistsAsync(timeout.Token));
        }
        finally
        {
            await fixture.TeardownAsync(CancellationToken.None);
        }
    }

    [Fact]
    public void FixtureGoSplitterPreservesBatchesAndRejectsInvalidRepeatCounts()
    {
        var batches = SqlServerFixtureGoSplitter.Split("SELECT 1;\n GO \nSELECT 2;\n\tGO\t2\n");

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
        Assert.Throws<FormatException>(() => SqlServerFixtureGoSplitter.Split("SELECT 1;\nGO 0\n"));
        Assert.Throws<FormatException>(() => SqlServerFixtureGoSplitter.Split("SELECT 1;\nGO twice\n"));
    }

    private static void AssertCatalogShape(TechnicalCatalog catalog)
    {
        Assert.Equal("1.0", catalog.CatalogVersion);
        Assert.Equal("sqlserver", catalog.Provider);

        string[] expectedIdentities =
        [
            "archive.Invoices",
            "crm.CustomerAddresses",
            "crm.Customers",
            "inventory.Categories",
            "inventory.Products",
            "inventory.StockBalances",
            "inventory.Warehouses",
            "operations.TypeCoverage",
            "reporting.InvoiceDetail",
            "reporting.InvoiceMonthlySummary",
            "sales.InvoiceLines",
            "sales.Invoices",
            "sales.InvoiceStatuses",
            "sales.Payments",
        ];
        Assert.Equal(
            expectedIdentities.OrderBy(identity => identity, StringComparer.Ordinal),
            catalog.Entities.Select(entity => entity.Identity.ToString()));
        Assert.DoesNotContain(catalog.Entities, entity => entity.Identity.Schema is "sys" or "INFORMATION_SCHEMA" or "unsupported");
        Assert.DoesNotContain(catalog.Entities, entity => entity.Identity.ToString() == "sales.InvoiceStatusesHistory");
        Assert.All(catalog.Entities, entity =>
        {
            Assert.Equal(entity.Fields.OrderBy(field => field.Ordinal).Select(field => field.Name), entity.Fields.Select(field => field.Name));
        });

        var customers = Entity(catalog, "crm", "Customers");
        Assert.Equal(CatalogObjectKind.Table, customers.Kind);
        var customerId = Field(customers, "CustomerId");
        Assert.Equal(1, customerId.Ordinal);
        Assert.Equal(CanonicalScalarType.Int32, customerId.CanonicalType);
        Assert.True(customerId.IsIdentity);
        AssertKey(customers, "PK_Customers", isPrimary: true, "CustomerId");
        AssertKey(customers, "UQ_Customers_CustomerCode", isPrimary: false, "CustomerCode");
        Assert.Equal(["UX_Customers_Email_WhenPresent"], customers.Indexes.Select(index => index.Name));
        var selfRelationship = Assert.Single(customers.Relationships, relationship => relationship.Name == "FK_Customers_Parent");
        Assert.Equal("crm.Customers", selfRelationship.Target.ToString());
        AssertPair(selfRelationship, "ParentCustomerId", "CustomerId");

        var invoices = Entity(catalog, "sales", "Invoices");
        Assert.Equal("Deterministic sales invoice fixture.", invoices.Description);
        var totalDue = Field(invoices, "TotalDue");
        Assert.True(totalDue.IsComputed);
        Assert.True(totalDue.IsPersistedComputed);
        Assert.Equal("Invoice total, persisted from component amounts.", totalDue.Description);
        Assert.Equal(CanonicalScalarType.Decimal, totalDue.CanonicalType);
        Assert.Equal(19, totalDue.ProviderType.Precision);
        Assert.Equal(4, totalDue.ProviderType.Scale);
        var rowVersion = Field(invoices, "RowVersion");
        Assert.True(rowVersion.IsRowVersion);
        AssertKey(invoices, "PK_Invoices", isPrimary: true, "InvoiceId");
        Assert.Empty(invoices.Indexes);
        Assert.Equal(4, invoices.Relationships.Count);
        AssertPair(Relationship(invoices, "FK_Invoices_BillToCustomer"), "BillToCustomerId", "CustomerId");
        AssertPair(Relationship(invoices, "FK_Invoices_ShipToCustomer"), "ShipToCustomerId", "CustomerId");
        var billAddress = Relationship(invoices, "FK_Invoices_BillToAddress");
        Assert.Equal("crm.CustomerAddresses", billAddress.Target.ToString());
        Assert.Equal(["BillToCustomerId", "BillToAddressKind"], billAddress.FieldPairs.Select(pair => pair.SourceField));
        Assert.Equal(["CustomerId", "AddressKind"], billAddress.FieldPairs.Select(pair => pair.TargetField));
        var shipAddress = Relationship(invoices, "FK_Invoices_ShipToAddress");
        Assert.Equal("crm.CustomerAddresses", shipAddress.Target.ToString());
        Assert.Equal(["ShipToCustomerId", "ShipToAddressKind"], shipAddress.FieldPairs.Select(pair => pair.SourceField));
        Assert.Equal(["CustomerId", "AddressKind"], shipAddress.FieldPairs.Select(pair => pair.TargetField));
        Assert.Equal(CanonicalScalarType.Binary, rowVersion.CanonicalType);

        var invoiceStatuses = Entity(catalog, "sales", "InvoiceStatuses");
        Assert.True(invoiceStatuses.IsTemporal);
        Assert.True(Field(invoiceStatuses, "ValidFrom").IsTemporalPeriodStart);
        Assert.True(Field(invoiceStatuses, "ValidTo").IsTemporalPeriodEnd);

        var typeCoverage = Entity(catalog, "operations", "TypeCoverage");
        Assert.Equal(CanonicalScalarType.Unknown, Field(typeCoverage, "XmlValue").CanonicalType);
        Assert.Equal(CanonicalScalarType.Unknown, Field(typeCoverage, "VariantValue").CanonicalType);
        Assert.Equal(CanonicalScalarType.Unknown, Field(typeCoverage, "HierarchyValue").CanonicalType);
        Assert.Equal(CanonicalScalarType.String, Field(typeCoverage, "JsonText").CanonicalType);
        Assert.True(Field(typeCoverage, "VersionValue").IsRowVersion);

        var stockBalances = Entity(catalog, "inventory", "StockBalances");
        AssertKey(stockBalances, "PK_StockBalances", isPrimary: true, "ProductId", "WarehouseId");

        var invoiceLines = Entity(catalog, "sales", "InvoiceLines");
        var standaloneIndex = Assert.Single(invoiceLines.Indexes, index => index.Name == "IX_InvoiceLines_Product_Quantity");
        Assert.False(standaloneIndex.IsUnique);
        Assert.False(standaloneIndex.IsFiltered);
        Assert.Equal(["ProductId", "Quantity"], standaloneIndex.Fields);
        Assert.Equal(["IX_InvoiceLines_Product_Quantity"], invoiceLines.Indexes.Select(index => index.Name));

        var filteredIndex = Assert.Single(customers.Indexes, index => index.Name == "UX_Customers_Email_WhenPresent");
        Assert.True(filteredIndex.IsUnique);
        Assert.True(filteredIndex.IsFiltered);
        Assert.Equal(["Email"], filteredIndex.Fields);

        var detailView = Entity(catalog, "reporting", "InvoiceDetail");
        Assert.Equal(CatalogObjectKind.View, detailView.Kind);
        Assert.Equal("Keyless invoice-line reporting view.", detailView.Description);
        Assert.Equal(CatalogObjectKind.View, Entity(catalog, "reporting", "InvoiceMonthlySummary").Kind);
        Assert.Empty(detailView.Keys);
        Assert.Empty(detailView.Relationships);
    }

    private static TechnicalEntity Entity(TechnicalCatalog catalog, string schema, string objectName) =>
        Assert.Single(catalog.Entities, entity =>
            string.Equals(entity.Identity.Schema, schema, StringComparison.Ordinal)
            && string.Equals(entity.Identity.ObjectName, objectName, StringComparison.Ordinal));

    private static TechnicalField Field(TechnicalEntity entity, string name) =>
        Assert.Single(entity.Fields, field => string.Equals(field.Name, name, StringComparison.Ordinal));

    private static CatalogRelationship Relationship(TechnicalEntity entity, string name) =>
        Assert.Single(entity.Relationships, relationship => string.Equals(relationship.Name, name, StringComparison.Ordinal));

    private static void AssertKey(TechnicalEntity entity, string name, bool isPrimary, params string[] fields)
    {
        var key = Assert.Single(entity.Keys, candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));
        Assert.Equal(isPrimary, key.IsPrimary);
        Assert.Equal(fields, key.Fields);
    }

    private static void AssertPair(CatalogRelationship relationship, string source, string target)
    {
        var pair = Assert.Single(relationship.FieldPairs);
        Assert.Equal(source, pair.SourceField);
        Assert.Equal(target, pair.TargetField);
    }
}
