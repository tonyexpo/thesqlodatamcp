using TheSqlODataMcp.Core.Catalog;
using TheSqlODataMcp.Persistence.CatalogStore;
using Xunit;

namespace TheSqlODataMcp.Persistence.Tests;

/// <summary>
/// Round-trip tests for <see cref="CatalogRevisionStore"/> against a real, file-backed SQLite database —
/// no mocks or in-memory stand-ins, mirroring this repository's real-disposable-resource testing
/// philosophy (ADR 0004) scaled down to an embedded database that needs no Docker.
/// </summary>
public sealed class CatalogRevisionStoreTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveThenGetRoundTripsASucceededRevisionExactly()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var store = new CatalogRevisionStore(fixture.DbContext);
        var catalog = CreateCatalog();
        var revision = CatalogRevisionFactory.Create(catalog, overlay: null, CreatedAt);

        var id = await store.SaveAsync(catalog, revision, timeout.Token);
        var stored = await store.GetAsync(id, timeout.Token);

        Assert.NotNull(stored);
        Assert.True(stored!.Succeeded);
        Assert.Equal(CreatedAt, stored.CreatedAt);
        Assert.Equal(revision.TechnicalHash, stored.TechnicalHash);
        Assert.Equal(revision.MergedHash, stored.MergedHash);
        Assert.Equal(TechnicalCatalogCanonicalJson.Serialize(catalog), stored.TechnicalCatalogJson);
        Assert.Equal(MergedCatalogCanonicalJson.Serialize(revision.MergedCatalog!), stored.MergedCatalogJson);
        Assert.Null(stored.ValidationResultJson);
    }

    [Fact]
    public async Task SaveThenGetRoundTripsAFailedRevisionAndItsErrorsExactly()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var store = new CatalogRevisionStore(fixture.DbContext);
        var catalog = CreateCatalog();
        var mismatchedOverlay = new SemanticOverlay("2.0", []);
        var revision = CatalogRevisionFactory.Create(catalog, mismatchedOverlay, CreatedAt);

        var id = await store.SaveAsync(catalog, revision, timeout.Token);
        var stored = await store.GetAsync(id, timeout.Token);

        Assert.NotNull(stored);
        Assert.False(stored!.Succeeded);
        Assert.Equal(revision.TechnicalHash, stored.TechnicalHash);
        Assert.Equal(TechnicalCatalogCanonicalJson.Serialize(catalog), stored.TechnicalCatalogJson);
        Assert.Null(stored.MergedHash);
        Assert.Null(stored.MergedCatalogJson);

        var roundTrippedErrors = CatalogRevisionMapper.DeserializeErrors(stored.ValidationResultJson!);
        var expectedError = Assert.Single(revision.Errors);
        var actualError = Assert.Single(roundTrippedErrors);
        Assert.Equal(expectedError.Code, actualError.Code);
        Assert.Equal(expectedError.Path, actualError.Path);
        Assert.Equal(expectedError.Message, actualError.Message);
    }

    [Fact]
    public async Task SaveThenGetRoundTripsNonAsciiOverlayTextByteForByteThroughSqlite()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var store = new CatalogRevisionStore(fixture.DbContext);
        var catalog = CreateCatalog();
        var overlay = new SemanticOverlay(
            "1.0",
            [new SemanticOverlayEntity(new PhysicalObjectIdentity("sales", "Customers"), displayName: "Città più importante — clientèle è già più ampia")]);
        var revision = CatalogRevisionFactory.Create(catalog, overlay, CreatedAt);

        var id = await store.SaveAsync(catalog, revision, timeout.Token);
        var stored = await store.GetAsync(id, timeout.Token);

        Assert.NotNull(stored);
        Assert.Equal(MergedCatalogCanonicalJson.Serialize(revision.MergedCatalog!), stored!.MergedCatalogJson);
        var roundTrippedDisplayName = System.Text.Json.JsonDocument.Parse(stored.MergedCatalogJson!)
            .RootElement.GetProperty("entities")[0].GetProperty("displayName").GetString();
        Assert.Equal("Città più importante — clientèle è già più ampia", roundTrippedDisplayName);
    }

    [Fact]
    public async Task GetReturnsNullForAnUnknownId()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var store = new CatalogRevisionStore(fixture.DbContext);

        var stored = await store.GetAsync(12345, timeout.Token);

        Assert.Null(stored);
    }

    [Fact]
    public async Task SaveAssignsIncreasingIdsAcrossMultipleRevisions()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var store = new CatalogRevisionStore(fixture.DbContext);
        var catalog = CreateCatalog();
        var revision = CatalogRevisionFactory.Create(catalog, overlay: null, CreatedAt);

        var firstId = await store.SaveAsync(catalog, revision, timeout.Token);
        var secondId = await store.SaveAsync(catalog, revision, timeout.Token);

        Assert.True(secondId > firstId);
    }

    [Fact]
    public async Task GetActiveReturnsNullWhenNothingHasEverBeenActivated()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var store = new CatalogRevisionStore(fixture.DbContext);
        var catalog = CreateCatalog();
        var revision = CatalogRevisionFactory.Create(catalog, overlay: null, CreatedAt);
        await store.SaveAsync(catalog, revision, timeout.Token);

        var active = await store.GetActiveAsync(timeout.Token);

        Assert.Null(active);
    }

    [Fact]
    public async Task ActivateThenGetActiveReturnsTheActivatedRevision()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var store = new CatalogRevisionStore(fixture.DbContext);
        var catalog = CreateCatalog();
        var revision = CatalogRevisionFactory.Create(catalog, overlay: null, CreatedAt);
        var id = await store.SaveAsync(catalog, revision, timeout.Token);
        var activatedAt = CreatedAt.AddMinutes(1);

        await store.ActivateAsync(id, activatedAt, timeout.Token);
        var active = await store.GetActiveAsync(timeout.Token);

        Assert.NotNull(active);
        Assert.Equal(id, active!.Id);
        Assert.Equal(activatedAt, active.ActivatedAt);
    }

    [Fact]
    public async Task ActivatingTheSameRevisionTwiceOverwritesItsActivatedAtTimestamp()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var store = new CatalogRevisionStore(fixture.DbContext);
        var catalog = CreateCatalog();
        var revision = CatalogRevisionFactory.Create(catalog, overlay: null, CreatedAt);
        var id = await store.SaveAsync(catalog, revision, timeout.Token);
        await store.ActivateAsync(id, CreatedAt, timeout.Token);

        await store.ActivateAsync(id, CreatedAt.AddMinutes(1), timeout.Token);
        var active = await store.GetActiveAsync(timeout.Token);

        Assert.Equal(id, active!.Id);
        Assert.Equal(CreatedAt.AddMinutes(1), active.ActivatedAt);
    }

    [Fact]
    public async Task ActivatingANewerRevisionSupersedesThePreviouslyActiveOne()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var store = new CatalogRevisionStore(fixture.DbContext);
        var catalog = CreateCatalog();
        var revision = CatalogRevisionFactory.Create(catalog, overlay: null, CreatedAt);
        var firstId = await store.SaveAsync(catalog, revision, timeout.Token);
        var secondId = await store.SaveAsync(catalog, revision, timeout.Token);
        await store.ActivateAsync(firstId, CreatedAt, timeout.Token);

        await store.ActivateAsync(secondId, CreatedAt.AddMinutes(1), timeout.Token);
        var active = await store.GetActiveAsync(timeout.Token);

        Assert.Equal(secondId, active!.Id);
    }

    [Fact]
    public async Task ActivateThrowsForAnUnknownId()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var store = new CatalogRevisionStore(fixture.DbContext);

        await Assert.ThrowsAsync<ArgumentException>(() => store.ActivateAsync(12345, CreatedAt, timeout.Token));
    }

    [Fact]
    public async Task ActivateThrowsForAFailedRevisionAndLeavesTheActiveRevisionUnchanged()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token);
        var store = new CatalogRevisionStore(fixture.DbContext);
        var catalog = CreateCatalog();
        var goodRevision = CatalogRevisionFactory.Create(catalog, overlay: null, CreatedAt);
        var goodId = await store.SaveAsync(catalog, goodRevision, timeout.Token);
        await store.ActivateAsync(goodId, CreatedAt, timeout.Token);
        var mismatchedOverlay = new SemanticOverlay("2.0", []);
        var failedRevision = CatalogRevisionFactory.Create(catalog, mismatchedOverlay, CreatedAt.AddMinutes(1));
        var failedId = await store.SaveAsync(catalog, failedRevision, timeout.Token);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ActivateAsync(failedId, CreatedAt.AddMinutes(1), timeout.Token));
        var active = await store.GetActiveAsync(timeout.Token);
        Assert.Equal(goodId, active!.Id);
    }

    [Fact]
    public async Task DisposingTheFixtureDeletesTheDatabaseFile()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        string databasePath;
        await using (var fixture = await SqliteControlStoreFixture.CreateAsync(timeout.Token))
        {
            databasePath = fixture.DatabasePath;
            Assert.True(File.Exists(databasePath));
        }

        Assert.False(File.Exists(databasePath));
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
