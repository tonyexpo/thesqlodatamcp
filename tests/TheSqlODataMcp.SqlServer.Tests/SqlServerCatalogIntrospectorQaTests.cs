using System.Reflection;
using System.Text.RegularExpressions;
using TheSqlODataMcp.Core.Catalog;
using TheSqlODataMcp.SqlServer;
using Xunit;

namespace TheSqlODataMcp.SqlServer.Tests;

public sealed class SqlServerCatalogIntrospectorQaTests
{
    [Fact]
    public void CatalogCommandIsOneFixedReadOnlySelectWithStructuralExclusions()
    {
        var queryField = typeof(SqlServerCatalogIntrospector).GetField(
            "CatalogQuery",
            BindingFlags.NonPublic | BindingFlags.Static);
        var query = Assert.IsType<string>(queryField?.GetRawConstantValue());

        var selectCount = Regex.Count(
            query,
            @"\bSELECT\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(selectCount == 1, $"Expected one SELECT statement but found {selectCount}.");
        Assert.DoesNotMatch(
            new Regex(
                @"\b(?:INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|CREATE|EXEC|EXECUTE)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            query);
        Assert.Contains("[sys].[objects]", query, StringComparison.Ordinal);
        Assert.Contains("[sys].[columns]", query, StringComparison.Ordinal);
        Assert.Contains("[sys].[types]", query, StringComparison.Ordinal);
        Assert.Contains("[ty].[user_type_id] = [c].[user_type_id]", query, StringComparison.Ordinal);
        Assert.Contains("[o].[type] IN ('U', 'V')", query, StringComparison.Ordinal);
        Assert.Contains("[o].[is_ms_shipped] = 0", query, StringComparison.Ordinal);
        Assert.Contains("N'INFORMATION_SCHEMA'", query, StringComparison.Ordinal);
        Assert.Contains("[t].[temporal_type], 0) <> 1", query, StringComparison.Ordinal);
        Assert.Contains("N'MS_Description'", query, StringComparison.Ordinal);
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
}
