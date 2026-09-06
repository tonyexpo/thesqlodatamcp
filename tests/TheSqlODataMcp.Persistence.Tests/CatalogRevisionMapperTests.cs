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

    [Fact]
    public void ToDomainRoundTripsASucceededRevisionIntoALiveCatalogSearchableByEntityAndAlias()
    {
        var catalog = CreateCatalog();
        var overlay = new SemanticOverlay(
            "1.0",
            [new SemanticOverlayEntity(new PhysicalObjectIdentity("sales", "Customers"), displayName: "Clienti", aliases: ["client-id"])]);
        var revision = CatalogRevisionFactory.Create(catalog, overlay, CreatedAt);
        var stored = CatalogRevisionMapper.ToStored(catalog, revision);

        var domain = CatalogRevisionMapper.ToDomain(stored);

        Assert.True(domain.Succeeded);
        Assert.Equal(revision.TechnicalHash, domain.TechnicalHash);
        Assert.Equal(revision.MergedHash, domain.MergedHash);
        Assert.Equal(MergedCatalogCanonicalJson.ComputeStructuralHash(revision.MergedCatalog!), MergedCatalogCanonicalJson.ComputeStructuralHash(domain.MergedCatalog!));

        var index = new CatalogSearchIndex(domain.MergedCatalog!);
        var found = index.FindEntity(new PhysicalObjectIdentity("sales", "Customers"));
        Assert.Equal("Clienti", found!.DisplayName);
        var match = Assert.Single(index.Search("client-id"));
        Assert.Equal(CatalogSearchMatchKind.EntityAlias, match.Kind);
    }

    [Fact]
    public void ToDomainRoundTripsAFailedRevisionsErrorsWithoutNeedingTheMergedCatalog()
    {
        var catalog = CreateCatalog();
        var mismatchedOverlay = new SemanticOverlay("2.0", []);
        var revision = CatalogRevisionFactory.Create(catalog, mismatchedOverlay, CreatedAt);
        var stored = CatalogRevisionMapper.ToStored(catalog, revision);

        var domain = CatalogRevisionMapper.ToDomain(stored);

        Assert.False(domain.Succeeded);
        Assert.Null(domain.MergedCatalog);
        Assert.Equal(revision.TechnicalHash, domain.TechnicalHash);
        var expected = Assert.Single(revision.Errors);
        var actual = Assert.Single(domain.Errors);
        Assert.Equal(expected.Code, actual.Code);
    }

    [Fact]
    public void ToDomainRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => CatalogRevisionMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainRejectsAStoredRowWhoseTechnicalCatalogJsonDoesNotMatchItsOwnTechnicalHash()
    {
        var catalog = CreateCatalog();
        var revision = CatalogRevisionFactory.Create(catalog, overlay: null, CreatedAt);
        var stored = CatalogRevisionMapper.ToStored(catalog, revision);
        var tampered = StoredCatalogRevision.Success(stored.CreatedAt, "not-the-real-technical-hash", stored.TechnicalCatalogJson, stored.MergedHash!, stored.MergedCatalogJson!);

        Assert.Throws<ArgumentException>(() => CatalogRevisionMapper.ToDomain(tampered));
    }

    [Fact]
    public void ToDomainRejectsAStoredRowWhoseMergedCatalogJsonDoesNotMatchItsOwnMergedHash()
    {
        var catalog = CreateCatalog();
        var revision = CatalogRevisionFactory.Create(catalog, overlay: null, CreatedAt);
        var stored = CatalogRevisionMapper.ToStored(catalog, revision);
        var tampered = StoredCatalogRevision.Success(stored.CreatedAt, stored.TechnicalHash, stored.TechnicalCatalogJson, "not-the-real-merged-hash", stored.MergedCatalogJson!);

        Assert.Throws<ArgumentException>(() => CatalogRevisionMapper.ToDomain(tampered));
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
