using System.Text.Json;
using TheSqlODataMcp.Core.Catalog;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

public sealed class TechnicalCatalogQaTests
{
    [Fact]
    public void IdentifiersPreserveCaseAndUseOrdinalIdentityComparison()
    {
        var upper = new TechnicalEntity(
            new PhysicalObjectIdentity("Sales", "Invoices"),
            CatalogObjectKind.Table,
            [Field("Id", 0), Field("id", 1)]);
        var lower = new TechnicalEntity(
            new PhysicalObjectIdentity("sales", "Invoices"),
            CatalogObjectKind.Table,
            [Field("Id", 0)]);

        var catalog = new TechnicalCatalog("1.0", "fixture", [lower, upper]);
        var json = TechnicalCatalogCanonicalJson.Serialize(catalog);

        Assert.Equal(2, catalog.Entities.Count);
        Assert.Contains("\"schema\":\"Sales\"", json, StringComparison.Ordinal);
        Assert.Contains("\"schema\":\"sales\"", json, StringComparison.Ordinal);
        Assert.True(
            json.IndexOf("\"schema\":\"Sales\"", StringComparison.Ordinal)
            < json.IndexOf("\"schema\":\"sales\"", StringComparison.Ordinal));
    }

    [Fact]
    public void StructuralHashIsLowercaseSha256AndIncludesProviderTypeDetails()
    {
        var narrow = CatalogWithProviderLength(50);
        var wide = CatalogWithProviderLength(100);

        var narrowHash = TechnicalCatalogCanonicalJson.ComputeStructuralHash(narrow);
        var wideHash = TechnicalCatalogCanonicalJson.ComputeStructuralHash(wide);

        Assert.Equal(64, narrowHash.Length);
        Assert.All(narrowHash, character => Assert.True(
            char.IsAsciiDigit(character) || character is >= 'a' and <= 'f'));
        Assert.NotEqual(narrowHash, wideHash);
        using var parsed = JsonDocument.Parse(TechnicalCatalogCanonicalJson.Serialize(narrow));
        Assert.Equal(50, parsed.RootElement
            .GetProperty("entities")[0]
            .GetProperty("fields")[0]
            .GetProperty("providerType")
            .GetProperty("length")
            .GetInt32());
    }

    [Fact]
    public void EntityRejectsInvalidEnumsDuplicateOrdinalsAndMultiplePrimaryKeys()
    {
        var identity = new PhysicalObjectIdentity("sales", "Invoices");
        var id = Field("Id", 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => new TechnicalField(
            "InvalidType",
            0,
            (CanonicalScalarType)int.MaxValue,
            new ProviderTypeDetails("int", "int"),
            isNullable: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TechnicalEntity(
            identity,
            (CatalogObjectKind)int.MaxValue,
            [id]));
        Assert.Throws<ArgumentException>(() => new TechnicalEntity(
            identity,
            CatalogObjectKind.Table,
            [id, Field("Other", 0)]));
        Assert.Throws<ArgumentException>(() => new TechnicalEntity(
            identity,
            CatalogObjectKind.Table,
            [id],
            [new CatalogKey("PK_One", ["Id"], isPrimary: true), new CatalogKey("PK_Two", ["Id"], isPrimary: true)]));
    }

    private static TechnicalCatalog CatalogWithProviderLength(int length)
    {
        var field = new TechnicalField(
            "Name",
            0,
            CanonicalScalarType.String,
            new ProviderTypeDetails("nvarchar", $"nvarchar({length})", length),
            isNullable: false);
        var entity = new TechnicalEntity(
            new PhysicalObjectIdentity("crm", "Customers"),
            CatalogObjectKind.Table,
            [field]);
        return new TechnicalCatalog("1.0", "fixture", [entity]);
    }

    private static TechnicalField Field(string name, int ordinal) => new(
        name,
        ordinal,
        CanonicalScalarType.Int32,
        new ProviderTypeDetails("int", "int"),
        isNullable: false);
}
