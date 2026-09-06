using TheSqlODataMcp.Core.Catalog;
using TheSqlODataMcp.Persistence.CatalogStore;
using Xunit;

namespace TheSqlODataMcp.Persistence.Tests;

/// <summary>Tests for <see cref="CatalogRevisionMapper"/> that do not require a database.</summary>
public sealed class CatalogRevisionMapperTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToStoredRejectsATechnicalCatalogThatDoesNotMatchTheRevisionsHash()
    {
        var catalog = CreateCatalog();
        var revision = CatalogRevisionFactory.Create(catalog, overlay: null, CreatedAt);
        var differentCatalog = new TechnicalCatalog("2.0", "fixture", catalog.Entities);

        Assert.Throws<ArgumentException>(() => CatalogRevisionMapper.ToStored(differentCatalog, revision));
    }

    [Fact]
    public void DeserializeErrorsRoundTripsCodePathAndMessage()
    {
        var catalog = CreateCatalog();
        var mismatchedOverlay = new SemanticOverlay("2.0", []);
        var revision = CatalogRevisionFactory.Create(catalog, mismatchedOverlay, CreatedAt);
        var stored = CatalogRevisionMapper.ToStored(catalog, revision);

        var roundTripped = CatalogRevisionMapper.DeserializeErrors(stored.ValidationResultJson!);

        var expected = Assert.Single(revision.Errors);
        var actual = Assert.Single(roundTripped);
        Assert.Equal(expected.Code, actual.Code);
        Assert.Equal(expected.Path, actual.Path);
        Assert.Equal(expected.Message, actual.Message);
    }

    private static TechnicalCatalog CreateCatalog()
    {
        var customers = new TechnicalEntity(
            new PhysicalObjectIdentity("sales", "Customers"),
            CatalogObjectKind.Table,
            [
                new TechnicalField("CustomerId", 0, CanonicalScalarType.Int32, new ProviderTypeDetails("int", "int"), isNullable: false, isIdentity: true),
                new TechnicalField("Name", 1, CanonicalScalarType.String, new ProviderTypeDetails("nvarchar", "nvarchar(100)", length: 100), isNullable: false),
            ],
            [new CatalogKey("PK_Customers", ["CustomerId"], isPrimary: true)]);

        return new TechnicalCatalog("1.0", "fixture", [customers]);
    }
}
