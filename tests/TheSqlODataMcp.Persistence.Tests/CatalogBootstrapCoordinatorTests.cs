using TheSqlODataMcp.Core.Catalog;
using TheSqlODataMcp.Persistence.CatalogStore;
using Xunit;

namespace TheSqlODataMcp.Persistence.Tests;

/// <summary>
/// End-to-end behavior tests for <see cref="CatalogBootstrapCoordinator"/> against a real SQLite database,
/// covering all four <see cref="CatalogBootstrapMode"/> values and the last-valid-rollback guarantee.
/// </summary>
public sealed class CatalogBootstrapCoordinatorTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DisabledNeverImportsEvenWhenEmpty()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var coordinator = new CatalogBootstrapCoordinator(new CatalogRevisionStore(fixture.DbContext));

        var result = await coordinator.RunAsync(CatalogBootstrapMode.Disabled, CreateCatalog("sales"), overlay: null, CreatedAt, timeout.Token);

        Assert.Null(result);
    }

    [Fact]
    public async Task ImportIfEmptyImportsOnceThenSkipsEvenIfTheCatalogChanges()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var coordinator = new CatalogBootstrapCoordinator(new CatalogRevisionStore(fixture.DbContext));

        var first = await coordinator.RunAsync(CatalogBootstrapMode.ImportIfEmpty, CreateCatalog("sales"), overlay: null, CreatedAt, timeout.Token);
        var second = await coordinator.RunAsync(CatalogBootstrapMode.ImportIfEmpty, CreateCatalog("marketing"), overlay: null, CreatedAt.AddMinutes(1), timeout.Token);

        Assert.NotNull(first);
        Assert.Equal(first!.Id, second!.Id);
        Assert.Equal(first.ActivatedAt, second.ActivatedAt);
    }

    [Fact]
    public async Task ImportIfChangedSkipsWhenTheTechnicalCatalogIsUnchanged()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var coordinator = new CatalogBootstrapCoordinator(new CatalogRevisionStore(fixture.DbContext));

        var first = await coordinator.RunAsync(CatalogBootstrapMode.ImportIfChanged, CreateCatalog("sales"), overlay: null, CreatedAt, timeout.Token);
        var second = await coordinator.RunAsync(CatalogBootstrapMode.ImportIfChanged, CreateCatalog("sales"), overlay: null, CreatedAt.AddMinutes(1), timeout.Token);

        Assert.Equal(first!.Id, second!.Id);
    }

    [Fact]
    public async Task ImportIfChangedRebuildsWhenTheTechnicalCatalogChanges()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var coordinator = new CatalogBootstrapCoordinator(new CatalogRevisionStore(fixture.DbContext));

        var first = await coordinator.RunAsync(CatalogBootstrapMode.ImportIfChanged, CreateCatalog("sales"), overlay: null, CreatedAt, timeout.Token);
        var second = await coordinator.RunAsync(CatalogBootstrapMode.ImportIfChanged, CreateCatalog("marketing"), overlay: null, CreatedAt.AddMinutes(1), timeout.Token);

        Assert.NotEqual(first!.Id, second!.Id);
        Assert.Equal(CreatedAt.AddMinutes(1), second.ActivatedAt);
    }

    [Fact]
    public async Task AlwaysImportRebuildsEveryTimeEvenWhenUnchanged()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var coordinator = new CatalogBootstrapCoordinator(new CatalogRevisionStore(fixture.DbContext));

        var first = await coordinator.RunAsync(CatalogBootstrapMode.AlwaysImport, CreateCatalog("sales"), overlay: null, CreatedAt, timeout.Token);
        var second = await coordinator.RunAsync(CatalogBootstrapMode.AlwaysImport, CreateCatalog("sales"), overlay: null, CreatedAt.AddMinutes(1), timeout.Token);

        Assert.NotEqual(first!.Id, second!.Id);
    }

    [Fact]
    public async Task AFailedRebuildPreservesThePreviouslyActiveRevision()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var coordinator = new CatalogBootstrapCoordinator(new CatalogRevisionStore(fixture.DbContext));
        var goodCatalog = CreateCatalog("sales");
        var good = await coordinator.RunAsync(CatalogBootstrapMode.AlwaysImport, goodCatalog, overlay: null, CreatedAt, timeout.Token);
        var mismatchedOverlay = new SemanticOverlay("2.0", []);

        var result = await coordinator.RunAsync(CatalogBootstrapMode.AlwaysImport, goodCatalog, mismatchedOverlay, CreatedAt.AddMinutes(1), timeout.Token);

        Assert.Equal(good!.Id, result!.Id);
        Assert.Equal(good.ActivatedAt, result.ActivatedAt);
    }

    private static TechnicalCatalog CreateCatalog(string schema)
    {
        var customers = new TechnicalEntity(
            new PhysicalObjectIdentity(schema, "Customers"),
            CatalogObjectKind.Table,
            [
                new TechnicalField("CustomerId", 0, CanonicalScalarType.Int32, new ProviderTypeDetails("int", "int"), isNullable: false, isIdentity: true),
                new TechnicalField("Name", 1, CanonicalScalarType.String, new ProviderTypeDetails("nvarchar", "nvarchar(100)", length: 100), isNullable: false),
            ],
            [new CatalogKey("PK_Customers", ["CustomerId"], isPrimary: true)]);

        return new TechnicalCatalog("1.0", "fixture", [customers]);
    }
}
