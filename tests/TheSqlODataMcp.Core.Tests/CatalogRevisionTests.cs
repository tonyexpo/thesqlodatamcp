using TheSqlODataMcp.Core.Catalog;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>
/// Construction-time invariant tests for <see cref="CatalogRevision"/>, constructed directly through its
/// <see cref="CatalogRevision.Success"/>/<see cref="CatalogRevision.Failure"/> factories rather than only
/// through <see cref="CatalogRevisionFactory"/>.
/// </summary>
public sealed class CatalogRevisionTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SucceededRevisionExposesTheMergedCatalogAndHashWithNoErrors()
    {
        var mergedCatalog = SimpleMergedCatalog();

        var revision = CatalogRevision.Success(CreatedAt, "technical-hash", mergedCatalog, "merged-hash");

        Assert.Equal(CatalogRevisionStatus.Succeeded, revision.Status);
        Assert.True(revision.Succeeded);
        Assert.Equal(CreatedAt, revision.CreatedAt);
        Assert.Equal("technical-hash", revision.TechnicalHash);
        Assert.Same(mergedCatalog, revision.MergedCatalog);
        Assert.Equal("merged-hash", revision.MergedHash);
        Assert.Empty(revision.Errors);
    }

    [Fact]
    public void FailedRevisionExposesTheErrorsWithNoMergedCatalogOrHash()
    {
        var error = new SemanticOverlayValidationError(CatalogMergeErrorCodes.EntitySourceNotFound, "$.entities[0].source", "not found");

        var revision = CatalogRevision.Failure(CreatedAt, "technical-hash", [error]);

        Assert.Equal(CatalogRevisionStatus.Failed, revision.Status);
        Assert.False(revision.Succeeded);
        Assert.Equal(CreatedAt, revision.CreatedAt);
        Assert.Equal("technical-hash", revision.TechnicalHash);
        Assert.Null(revision.MergedCatalog);
        Assert.Null(revision.MergedHash);
        Assert.Same(error, Assert.Single(revision.Errors));
    }

    [Fact]
    public void SucceededRejectsWhitespaceHashesAndNullCatalog()
    {
        var mergedCatalog = SimpleMergedCatalog();

        Assert.ThrowsAny<ArgumentException>(() => CatalogRevision.Success(CreatedAt, " ", mergedCatalog, "merged-hash"));
        Assert.ThrowsAny<ArgumentException>(() => CatalogRevision.Success(CreatedAt, "technical-hash", mergedCatalog, " "));
        Assert.Throws<ArgumentNullException>(() => CatalogRevision.Success(CreatedAt, "technical-hash", null!, "merged-hash"));
    }

    [Fact]
    public void FailedRejectsWhitespaceHashEmptyErrorsAndNullErrorEntries()
    {
        var error = new SemanticOverlayValidationError(CatalogMergeErrorCodes.EntitySourceNotFound, "$.entities[0].source", "not found");

        Assert.ThrowsAny<ArgumentException>(() => CatalogRevision.Failure(CreatedAt, " ", [error]));
        Assert.Throws<ArgumentException>(() => CatalogRevision.Failure(CreatedAt, "technical-hash", []));
        Assert.Throws<ArgumentException>(() => CatalogRevision.Failure(CreatedAt, "technical-hash", [null!]));
        Assert.Throws<ArgumentNullException>(() => CatalogRevision.Failure(CreatedAt, "technical-hash", null!));
    }

    private static MergedCatalog SimpleMergedCatalog()
    {
        var physical = new TechnicalEntity(
            new PhysicalObjectIdentity("sales", "Customers"),
            CatalogObjectKind.Table,
            [new TechnicalField("CustomerId", 0, CanonicalScalarType.Int32, new ProviderTypeDetails("int", "int"), isNullable: false, isIdentity: true)],
            [new CatalogKey("PK_Customers", ["CustomerId"], isPrimary: true)]);
        var entity = new MergedEntity(physical, "Customers", [], effectiveKeyFields: ["CustomerId"]);

        return new MergedCatalog("1.0", "fixture", configured: false, [entity]);
    }
}
