using System.Reflection;
using System.Text.RegularExpressions;
using TheSqlODataMcp.Core.Catalog;
using TheSqlODataMcp.SqlServer;
using Xunit;

namespace TheSqlODataMcp.SqlServer.Tests;

public sealed class SqlServerCatalogIntrospectorQaTests
{
    [Fact]
    public void CatalogCommandIsFixedReadOnlyAndReturnsExactlyTheThreeExpectedMetadataResultSets()
    {
        var queryField = typeof(SqlServerCatalogIntrospector).GetField(
            "CatalogQuery",
            BindingFlags.NonPublic | BindingFlags.Static);
        var query = Assert.IsType<string>(queryField?.GetRawConstantValue());

        Assert.DoesNotMatch(
            new Regex(
                @"\b(?:INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|CREATE|EXEC|EXECUTE)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            query);
        Assert.Equal(3, Regex.Count(query, @";\s*(?=SELECT|\z)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        Assert.Contains("[sys].[objects]", query, StringComparison.Ordinal);
        Assert.Contains("[sys].[columns]", query, StringComparison.Ordinal);
        Assert.Contains("[sys].[types]", query, StringComparison.Ordinal);
        Assert.Contains("[ty].[user_type_id] = [c].[user_type_id]", query, StringComparison.Ordinal);
        Assert.Contains("CONVERT(varchar(1), [o].[type]) AS [ObjectType]", query, StringComparison.Ordinal);
        Assert.Contains("[o].[type] IN ('U', 'V')", query, StringComparison.Ordinal);
        Assert.Contains("[o].[is_ms_shipped] = 0", query, StringComparison.Ordinal);
        Assert.Contains("N'INFORMATION_SCHEMA'", query, StringComparison.Ordinal);
        Assert.Contains("[t].[temporal_type], 0) <> 1", query, StringComparison.Ordinal);
        Assert.Contains("N'MS_Description'", query, StringComparison.Ordinal);
        Assert.Contains("[sys].[key_constraints]", query, StringComparison.Ordinal);
        Assert.Contains("[sys].[indexes]", query, StringComparison.Ordinal);
        Assert.Contains("[sys].[foreign_keys]", query, StringComparison.Ordinal);
        Assert.Contains("[sys].[foreign_key_columns]", query, StringComparison.Ordinal);
        Assert.Contains("[i].[type] IN (1, 2)", query, StringComparison.Ordinal);
        Assert.Contains("[i].[is_hypothetical] = 0", query, StringComparison.Ordinal);
        Assert.Contains("[i].[is_disabled] = 0", query, StringComparison.Ordinal);
        Assert.Contains("[ic].[is_included_column] = 0", query, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(query, @"CONVERT\(int, \[ic\]\.\[key_ordinal\]\) AS \[KeyOrdinal\]", RegexOptions.CultureInvariant));
        Assert.Contains("[fkc].[constraint_column_id] AS [PairOrdinal]", query, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectionKeepsUnrecognizedUserTypeExplicitlyUnknown()
    {
        var catalog = SqlServerCatalogProjection.CreateCatalog(
        [
            new SqlServerCatalogColumnMetadata(
                1,
                "custom",
                "AliasCoverage",
                "U",
                0,
                "AliasValue",
                1,
                false,
                false,
                false,
                false,
                0,
                56,
                "FixtureAlias",
                4,
                10,
                0,
                null,
                null),
        ]);

        var field = Assert.Single(Assert.Single(catalog.Entities).Fields);
        Assert.Equal(CanonicalScalarType.Unknown, field.CanonicalType);
        Assert.Equal("fixturealias", field.ProviderType.Name);
        Assert.Null(field.ProviderType.Length);
        Assert.Null(field.ProviderType.Precision);
        Assert.Null(field.ProviderType.Scale);
    }

    [Fact]
    public void RelationalMetadataProjectionIsIndependentOfCatalogRowOrder()
    {
        SqlServerCatalogColumnMetadata[] columns =
        [
            Column(20, "sales", "Invoices", "InvoiceId", 1),
            Column(10, "crm", "Customers", "CustomerCode", 2, "varchar", 16, 0),
            Column(20, "sales", "Invoices", "CustomerCode", 2, "varchar", 16, 0),
            Column(10, "crm", "Customers", "CustomerId", 1),
        ];
        SqlServerCatalogKeyOrIndexMetadata[] keysAndIndexes =
        [
            new(20, "I", "IX_Invoices_Customer", false, false, false, 2, "InvoiceId"),
            new(10, "K", "PK_Customers", true, true, false, 1, "CustomerId"),
            new(20, "K", "PK_Invoices", true, true, false, 1, "InvoiceId"),
            new(20, "I", "IX_Invoices_Customer", false, false, false, 1, "CustomerCode"),
        ];
        SqlServerCatalogForeignKeyMetadata[] foreignKeys =
        [
            new(20, "FK_Invoices_Customers", 10, "crm", "Customers", 2, "CustomerCode", "CustomerCode"),
            new(20, "FK_Invoices_Customers", 10, "crm", "Customers", 1, "InvoiceId", "CustomerId"),
        ];

        var forward = SqlServerCatalogProjection.CreateCatalog(columns, keysAndIndexes, foreignKeys);
        var reverse = SqlServerCatalogProjection.CreateCatalog(
            columns.Reverse(),
            keysAndIndexes.Reverse(),
            foreignKeys.Reverse());

        Assert.Equal(
            TechnicalCatalogCanonicalJson.Serialize(forward),
            TechnicalCatalogCanonicalJson.Serialize(reverse));
        Assert.Equal(
            TechnicalCatalogCanonicalJson.ComputeStructuralHash(forward),
            TechnicalCatalogCanonicalJson.ComputeStructuralHash(reverse));
    }

    private static SqlServerCatalogColumnMetadata Column(
        int objectId,
        string schema,
        string objectName,
        string columnName,
        int ordinal,
        string providerTypeName = "int",
        int maxLength = 4,
        int precision = 10) =>
        new(
            objectId,
            schema,
            objectName,
            "U",
            0,
            columnName,
            ordinal,
            false,
            false,
            false,
            false,
            0,
            providerTypeName == "int" ? 56 : 167,
            providerTypeName,
            maxLength,
            precision,
            0,
            null,
            null);
}
