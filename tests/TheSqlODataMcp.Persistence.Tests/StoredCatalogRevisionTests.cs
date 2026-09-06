using TheSqlODataMcp.Core.Catalog;
using TheSqlODataMcp.Persistence.CatalogStore;
using Xunit;

namespace TheSqlODataMcp.Persistence.Tests;

/// <summary>Construction-time invariant tests for <see cref="StoredCatalogRevision"/>.</summary>
public sealed class StoredCatalogRevisionTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SuccessFactoryProducesASucceededRowWithNoValidationResult()
    {
        var stored = StoredCatalogRevision.Success(CreatedAt, "tech-hash", "{}", "merged-hash", "{}");

        Assert.True(stored.Succeeded);
        Assert.Equal(CatalogRevisionStatus.Succeeded, stored.Status);
        Assert.Null(stored.ValidationResultJson);
    }

    [Fact]
    public void FailureFactoryProducesAFailedRowWithNoMergedData()
    {
        var stored = StoredCatalogRevision.Failure(CreatedAt, "tech-hash", "{}", "[]");

        Assert.False(stored.Succeeded);
        Assert.Equal(CatalogRevisionStatus.Failed, stored.Status);
        Assert.Null(stored.MergedHash);
        Assert.Null(stored.MergedCatalogJson);
    }

    [Fact]
    public void ConstructorRejectsASucceededRowMissingAMergedHash()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StoredCatalogRevision(CreatedAt, CatalogRevisionStatus.Succeeded, "tech-hash", "{}", mergedHash: null, mergedCatalogJson: "{}", validationResultJson: null));
    }

    [Fact]
    public void ConstructorRejectsASucceededRowCarryingAValidationResult()
    {
        Assert.Throws<ArgumentException>(() =>
            new StoredCatalogRevision(CreatedAt, CatalogRevisionStatus.Succeeded, "tech-hash", "{}", "merged-hash", "{}", validationResultJson: "[]"));
    }

    [Fact]
    public void ConstructorRejectsAFailedRowMissingAValidationResult()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StoredCatalogRevision(CreatedAt, CatalogRevisionStatus.Failed, "tech-hash", "{}", mergedHash: null, mergedCatalogJson: null, validationResultJson: null));
    }

    [Fact]
    public void ConstructorRejectsAFailedRowCarryingAMergedHash()
    {
        Assert.Throws<ArgumentException>(() =>
            new StoredCatalogRevision(CreatedAt, CatalogRevisionStatus.Failed, "tech-hash", "{}", mergedHash: "merged-hash", mergedCatalogJson: null, validationResultJson: "[]"));
    }

    [Fact]
    public void ConstructorRejectsAWhitespaceOnlyTechnicalHash()
    {
        Assert.Throws<ArgumentException>(() => StoredCatalogRevision.Success(CreatedAt, "   ", "{}", "merged-hash", "{}"));
    }

    [Fact]
    public void ActivateSetsActivatedAtOnASucceededRow()
    {
        var stored = StoredCatalogRevision.Success(CreatedAt, "tech-hash", "{}", "merged-hash", "{}");
        var activatedAt = CreatedAt.AddMinutes(5);

        stored.Activate(activatedAt);

        Assert.Equal(activatedAt, stored.ActivatedAt);
    }

    [Fact]
    public void ActivateRejectsAFailedRow()
    {
        var stored = StoredCatalogRevision.Failure(CreatedAt, "tech-hash", "{}", "[]");

        Assert.Throws<InvalidOperationException>(() => stored.Activate(CreatedAt));
        Assert.Null(stored.ActivatedAt);
    }
}
