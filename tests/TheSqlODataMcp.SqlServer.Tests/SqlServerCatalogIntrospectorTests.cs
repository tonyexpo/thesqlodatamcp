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

    [Fact]
    public void ProjectsOrderedKeysStandaloneIndexesAndMultipleCompositeRelationships()
    {
        SqlServerCatalogColumnMetadata[] rows =
        [
            Row(10, "crm", "Customers", "U", 0, "CustomerId", 1, "int", 4, 10, 0),
            Row(10, "crm", "Customers", "U", 0, "CustomerCode", 2, "varchar", 16, 0, 0),
            Row(20, "sales", "Invoices", "U", 0, "InvoiceId", 1, "int", 4, 10, 0),
            Row(20, "sales", "Invoices", "U", 0, "BillToCustomerId", 2, "int", 4, 10, 0),
            Row(20, "sales", "Invoices", "U", 0, "ShipToCustomerId", 3, "int", 4, 10, 0),
            Row(20, "sales", "Invoices", "U", 0, "LegacyCustomerCode", 4, "varchar", 16, 0, 0),
        ];
        SqlServerCatalogKeyOrIndexMetadata[] keyAndIndexRows =
        [
            Key(10, "K", "UQ_Customers_Code", false, true, false, 1, "CustomerCode"),
            Key(10, "K", "PK_Customers", true, true, false, 1, "CustomerId"),
            Key(20, "K", "PK_Invoices", true, true, false, 1, "InvoiceId"),
            Key(20, "I", "IX_Invoices_Customer_Code", false, false, false, 2, "LegacyCustomerCode"),
            Key(20, "I", "IX_Invoices_Customer_Code", false, false, false, 1, "BillToCustomerId"),
            Key(20, "I", "UX_Invoices_Ship", false, true, true, 1, "ShipToCustomerId"),
        ];
        SqlServerCatalogForeignKeyMetadata[] foreignKeyRows =
        [
            ForeignKey(20, "FK_Invoices_Bill", 10, "crm", "Customers", 1, "BillToCustomerId", "CustomerId"),
            ForeignKey(20, "FK_Invoices_Ship", 10, "crm", "Customers", 1, "ShipToCustomerId", "CustomerId"),
        ];

        var catalog = SqlServerCatalogProjection.CreateCatalog(rows, keyAndIndexRows, foreignKeyRows);
        var invoices = Assert.Single(catalog.Entities, entity => entity.Identity.ToString() == "sales.Invoices");

        Assert.Equal(["PK_Invoices"], invoices.Keys.Select(key => key.Name));
        Assert.Equal(["IX_Invoices_Customer_Code", "UX_Invoices_Ship"], invoices.Indexes.Select(index => index.Name));
        Assert.Equal(["BillToCustomerId", "LegacyCustomerCode"], invoices.Indexes[0].Fields);
        Assert.False(invoices.Indexes[0].IsUnique);
        Assert.True(invoices.Indexes[1].IsUnique);
        Assert.True(invoices.Indexes[1].IsFiltered);
        Assert.Equal(["FK_Invoices_Bill", "FK_Invoices_Ship"], invoices.Relationships.Select(relationship => relationship.Name));
        Assert.All(invoices.Relationships, relationship => Assert.Equal("crm.Customers", relationship.Target.ToString()));
    }

    [Fact]
    public void RejectsForeignKeysWhoseTargetIsNotDiscovered()
    {
        SqlServerCatalogColumnMetadata[] rows = [Row(10, "sales", "Invoices", "U", 0, "CustomerId", 1, "int", 4, 10, 0)];
        SqlServerCatalogForeignKeyMetadata[] foreignKeyRows = [ForeignKey(10, "FK_Invoices_Customers", 99, "crm", "Customers", 1, "CustomerId", "CustomerId")];

        Assert.Throws<InvalidOperationException>(() => SqlServerCatalogProjection.CreateCatalog(
            rows,
            Array.Empty<SqlServerCatalogKeyOrIndexMetadata>(),
            foreignKeyRows));
    }

    [Fact]
    public void RejectsForeignKeysWhoseTargetFieldIsNotDiscovered()
    {
        SqlServerCatalogColumnMetadata[] rows =
        [
            Row(10, "crm", "Customers", "U", 0, "CustomerId", 1, "int", 4, 10, 0),
            Row(20, "sales", "Invoices", "U", 0, "CustomerId", 1, "int", 4, 10, 0),
        ];
        SqlServerCatalogForeignKeyMetadata[] foreignKeyRows =
        [
            ForeignKey(20, "FK_Invoices_Customers", 10, "crm", "Customers", 1, "CustomerId", "MissingTarget"),
        ];

        Assert.Throws<InvalidOperationException>(() => SqlServerCatalogProjection.CreateCatalog(
            rows,
            Array.Empty<SqlServerCatalogKeyOrIndexMetadata>(),
            foreignKeyRows));
    }

    [Fact]
    public void RejectsKeyOrIndexMetadataWhoseObjectIsNotDiscovered()
    {
        SqlServerCatalogColumnMetadata[] rows = [Row(10, "sales", "Invoices", "U", 0, "InvoiceId", 1, "int", 4, 10, 0)];
        SqlServerCatalogKeyOrIndexMetadata[] keyAndIndexRows = [Key(99, "K", "PK_Missing", true, true, false, 1, "InvoiceId")];

        Assert.Throws<InvalidOperationException>(() => SqlServerCatalogProjection.CreateCatalog(rows, keyAndIndexRows));
    }

    [Fact]
    public void RejectsForeignKeyMetadataWhoseSourceIsNotDiscovered()
    {
        SqlServerCatalogColumnMetadata[] rows = [Row(10, "crm", "Customers", "U", 0, "CustomerId", 1, "int", 4, 10, 0)];
        SqlServerCatalogForeignKeyMetadata[] foreignKeyRows = [ForeignKey(99, "FK_Missing_Customers", 10, "crm", "Customers", 1, "CustomerId", "CustomerId")];

        Assert.Throws<InvalidOperationException>(() => SqlServerCatalogProjection.CreateCatalog(
            rows,
            Array.Empty<SqlServerCatalogKeyOrIndexMetadata>(),
            foreignKeyRows));
    }

    [Fact]
    public void RejectsGapsInKeyIndexAndForeignKeyPairOrdinals()
    {
        SqlServerCatalogColumnMetadata[] rows =
        [
            Row(10, "crm", "Customers", "U", 0, "CustomerId", 1, "int", 4, 10, 0),
            Row(10, "crm", "Customers", "U", 0, "CustomerCode", 2, "varchar", 16, 0, 0),
            Row(20, "sales", "Invoices", "U", 0, "CustomerId", 1, "int", 4, 10, 0),
            Row(20, "sales", "Invoices", "U", 0, "CustomerCode", 2, "varchar", 16, 0, 0),
        ];

        SqlServerCatalogKeyOrIndexMetadata[] gapInIndex =
        [
            Key(20, "I", "IX_Invoices_Customer", false, false, false, 1, "CustomerId"),
            Key(20, "I", "IX_Invoices_Customer", false, false, false, 3, "CustomerCode"),
        ];
        Assert.Throws<InvalidOperationException>(() => SqlServerCatalogProjection.CreateCatalog(rows, gapInIndex));

        SqlServerCatalogForeignKeyMetadata[] gapInForeignKey =
        [
            ForeignKey(20, "FK_Invoices_Customers", 10, "crm", "Customers", 1, "CustomerId", "CustomerId"),
            ForeignKey(20, "FK_Invoices_Customers", 10, "crm", "Customers", 3, "CustomerCode", "CustomerCode"),
        ];
        Assert.Throws<InvalidOperationException>(() => SqlServerCatalogProjection.CreateCatalog(
            rows,
            Array.Empty<SqlServerCatalogKeyOrIndexMetadata>(),
            gapInForeignKey));
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

    private static SqlServerCatalogKeyOrIndexMetadata Key(
        int objectId,
        string kind,
        string name,
        bool isPrimary,
        bool isUnique,
        bool isFiltered,
        int ordinal,
        string columnName) =>
        new(objectId, kind, name, isPrimary, isUnique, isFiltered, ordinal, columnName);

    private static SqlServerCatalogForeignKeyMetadata ForeignKey(
        int sourceObjectId,
        string name,
        int targetObjectId,
        string targetSchema,
        string targetObject,
        int ordinal,
        string sourceColumn,
        string targetColumn) =>
        new(sourceObjectId, name, targetObjectId, targetSchema, targetObject, ordinal, sourceColumn, targetColumn);
}
