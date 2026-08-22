using TheSqlODataMcp.Core.Catalog;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>
/// Behavior tests for <see cref="CatalogRevisionFactory"/>: the first production consumer of
/// <see cref="CatalogRevision"/>, tying together <see cref="CatalogMerger"/> and both canonical-JSON
/// structural hashes.
/// </summary>
public sealed class CatalogRevisionFactoryTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateWithNoOverlaySucceedsWithAnUnconfiguredMergedCatalogAndMatchingHashes()
    {
        var catalog = CreateCatalog();

        var revision = CatalogRevisionFactory.Create(catalog, overlay: null, CreatedAt);

        Assert.True(revision.Succeeded);
        Assert.Equal(CreatedAt, revision.CreatedAt);
        Assert.Equal(TechnicalCatalogCanonicalJson.ComputeStructuralHash(catalog), revision.TechnicalHash);
        Assert.False(revision.MergedCatalog!.Configured);
        Assert.Equal(MergedCatalogCanonicalJson.ComputeStructuralHash(revision.MergedCatalog!), revision.MergedHash);
        Assert.Empty(revision.Errors);
    }

    [Fact]
    public void CreateWithAValidOverlaySucceedsWithAConfiguredMergedCatalog()
    {
        var catalog = CreateCatalog();
        var overlay = new SemanticOverlay(
            "1.0",
            [new SemanticOverlayEntity(new PhysicalObjectIdentity("sales", "Customers"), displayName: "Clienti")]);

        var revision = CatalogRevisionFactory.Create(catalog, overlay, CreatedAt);

        Assert.True(revision.Succeeded);
        Assert.True(revision.MergedCatalog!.Configured);
        Assert.Equal(
            "Clienti",
            revision.MergedCatalog!.Entities.Single(e => e.Physical.Identity.ObjectName == "Customers").DisplayName);
        Assert.Equal(MergedCatalogCanonicalJson.ComputeStructuralHash(revision.MergedCatalog!), revision.MergedHash);
    }

    [Fact]
    public void CreateWithAnInvalidOverlayFailsButStillComputesTheTechnicalHash()
    {
        var catalog = CreateCatalog();
        var mismatchedOverlay = new SemanticOverlay("2.0", []);

        var revision = CatalogRevisionFactory.Create(catalog, mismatchedOverlay, CreatedAt);

        Assert.False(revision.Succeeded);
        Assert.Equal(TechnicalCatalogCanonicalJson.ComputeStructuralHash(catalog), revision.TechnicalHash);
        Assert.Null(revision.MergedCatalog);
        Assert.Null(revision.MergedHash);
        var error = Assert.Single(revision.Errors);
        Assert.Equal(CatalogMergeErrorCodes.CatalogVersionMismatch, error.Code);
    }

    [Fact]
    public void CreateRejectsNullCatalog()
    {
        Assert.Throws<ArgumentNullException>(() => CatalogRevisionFactory.Create(null!, overlay: null, CreatedAt));
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
