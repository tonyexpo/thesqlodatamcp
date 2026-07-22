using System.Reflection;
using TheSqlODataMcp.Core.Catalog;
using TheSqlODataMcp.SqlServer;
using Xunit;

namespace TheSqlODataMcp.SqlServer.Tests;

public sealed class SqlServerCatalogIntrospectorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConstructorRejectsBlankConnectionStrings(string? connectionString)
    {
        Assert.ThrowsAny<ArgumentException>(() => new SqlServerCatalogIntrospector(connectionString!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRequiresPositiveCommandTimeout(int commandTimeoutSeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SqlServerCatalogIntrospector("Server=(local);Database=fixture;", commandTimeoutSeconds));
    }

    [Fact]
    public void PublicIntrospectorApiDoesNotExposeSqlClientTypes()
    {
        var publicApiTypes = typeof(SqlServerCatalogIntrospector).Assembly.ExportedTypes
            .SelectMany(type =>
                type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)
                        .Append(method.ReturnType))
                    .Concat(type.GetConstructors().SelectMany(constructor =>
                        constructor.GetParameters().Select(parameter => parameter.ParameterType)))
                    .Concat(type.GetProperties().Select(property => property.PropertyType)))
            .Distinct();

        Assert.DoesNotContain(publicApiTypes, type =>
            string.Equals(type.Assembly.GetName().Name, "Microsoft.Data.SqlClient", StringComparison.Ordinal));
    }

    [Fact]
    public void ProjectsFieldsAndEntitiesWithStableOrdinalOrdering()
    {
        var catalog = SqlServerCatalogProjection.CreateCatalog(
        [
            Row(20, "sales", "Invoices", "U", 0, "TotalDue", 3, "decimal", 9, 18, 4,
                isComputed: true, isPersistedComputed: true, objectDescription: "Invoice data", columnDescription: "Persisted total"),
            Row(10, "crm", "Customers", "U", 0, "DisplayName", 2, "nvarchar", 240, 0, 0),
            Row(20, "sales", "Invoices", "U", 0, "InvoiceId", 1, "int", 4, 10, 0,
                isIdentity: true, objectDescription: "Invoice data"),
            Row(10, "crm", "Customers", "U", 0, "CustomerId", 1, "int", 4, 10, 0,
                isIdentity: true),
        ]);

        Assert.Equal("1.0", catalog.CatalogVersion);
        Assert.Equal("sqlserver", catalog.Provider);
        Assert.Collection(
            catalog.Entities,
            customer =>
            {
                Assert.Equal("crm", customer.Identity.Schema);
                Assert.Equal("Customers", customer.Identity.ObjectName);
                Assert.Equal(["CustomerId", "DisplayName"], customer.Fields.Select(field => field.Name));
            },
            invoice =>
            {
                Assert.Equal("sales", invoice.Identity.Schema);
                Assert.Equal("Invoices", invoice.Identity.ObjectName);
                Assert.Equal("Invoice data", invoice.Description);
                Assert.Empty(invoice.Keys);
                Assert.Empty(invoice.Indexes);
                Assert.Empty(invoice.Relationships);

                var total = invoice.Fields[1];
                Assert.Equal(3, total.Ordinal);
                Assert.Equal(CanonicalScalarType.Decimal, total.CanonicalType);
                Assert.True(total.IsComputed);
                Assert.True(total.IsPersistedComputed);
                Assert.Equal("Persisted total", total.Description);
            });
    }

    [Fact]
    public void ProjectsTemporalAndRowVersionMetadata()
    {
        var catalog = SqlServerCatalogProjection.CreateCatalog(
        [
            Row(30, "sales", "InvoiceStatuses", "U", 2, "InvoiceId", 1, "int", 4, 10, 0),
            Row(30, "sales", "InvoiceStatuses", "U", 2, "ValidFrom", 2, "datetime2", 8, 27, 7, generatedAlwaysType: 1),
            Row(30, "sales", "InvoiceStatuses", "U", 2, "ValidTo", 3, "datetime2", 8, 27, 7, generatedAlwaysType: 2),
            Row(30, "sales", "InvoiceStatuses", "U", 2, "VersionValue", 4, "timestamp", 8, 0, 0, systemTypeId: 189),
        ]);

        var entity = Assert.Single(catalog.Entities);
        Assert.True(entity.IsTemporal);
        Assert.True(entity.Fields[1].IsTemporalPeriodStart);
        Assert.True(entity.Fields[2].IsTemporalPeriodEnd);
        Assert.True(entity.Fields[3].IsRowVersion);
        Assert.Equal(CanonicalScalarType.Binary, entity.Fields[3].CanonicalType);
    }

    [Fact]
    public void RejectsInconsistentObjectMetadata()
    {
        var first = Row(10, "crm", "Customers", "U", 0, "CustomerId", 1, "int", 4, 10, 0);
        var second = Row(10, "sales", "Customers", "U", 0, "DisplayName", 2, "nvarchar", 240, 0, 0);

        Assert.Throws<InvalidOperationException>(() => SqlServerCatalogProjection.CreateCatalog([first, second]));
    }

    private static SqlServerCatalogColumnMetadata Row(
        int objectId,
        string schema,
        string objectName,
        string objectType,
        int temporalType,
        string columnName,
        int columnId,
        string providerTypeName,
        int maxLength,
        int precision,
        int scale,
        bool isNullable = false,
        bool isIdentity = false,
        bool isComputed = false,
        bool isPersistedComputed = false,
        int generatedAlwaysType = 0,
        int systemTypeId = 0,
        string? objectDescription = null,
        string? columnDescription = null) =>
        new(
            objectId,
            schema,
            objectName,
            objectType,
            temporalType,
            columnName,
            columnId,
            isNullable,
            isIdentity,
            isComputed,
            isPersistedComputed,
            generatedAlwaysType,
            systemTypeId,
            providerTypeName,
            maxLength,
            precision,
            scale,
            objectDescription,
            columnDescription);
}
