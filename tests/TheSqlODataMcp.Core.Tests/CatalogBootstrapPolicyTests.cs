using TheSqlODataMcp.Core.Catalog;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>Behavior tests for <see cref="CatalogBootstrapPolicy"/>'s pure decision table.</summary>
public sealed class CatalogBootstrapPolicyTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DisabledNeverImports(bool hasActiveRevision, bool technicalCatalogChanged)
    {
        Assert.False(CatalogBootstrapPolicy.ShouldImport(CatalogBootstrapMode.Disabled, hasActiveRevision, technicalCatalogChanged));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    public void ImportIfEmptyOnlyImportsWhenNoRevisionIsActiveYet(bool hasActiveRevision, bool technicalCatalogChanged, bool expected)
    {
        Assert.Equal(expected, CatalogBootstrapPolicy.ShouldImport(CatalogBootstrapMode.ImportIfEmpty, hasActiveRevision, technicalCatalogChanged));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void ImportIfChangedImportsWhenEmptyOrChanged(bool hasActiveRevision, bool technicalCatalogChanged, bool expected)
    {
        Assert.Equal(expected, CatalogBootstrapPolicy.ShouldImport(CatalogBootstrapMode.ImportIfChanged, hasActiveRevision, technicalCatalogChanged));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AlwaysImportAlwaysImports(bool hasActiveRevision, bool technicalCatalogChanged)
    {
        Assert.True(CatalogBootstrapPolicy.ShouldImport(CatalogBootstrapMode.AlwaysImport, hasActiveRevision, technicalCatalogChanged));
    }

    [Fact]
    public void ShouldImportRejectsAnUndefinedMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CatalogBootstrapPolicy.ShouldImport((CatalogBootstrapMode)999, false, false));
    }
}
