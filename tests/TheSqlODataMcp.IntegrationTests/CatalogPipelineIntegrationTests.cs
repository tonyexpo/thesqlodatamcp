using Microsoft.EntityFrameworkCore;
using TheSqlODataMcp.Core.Catalog;
using TheSqlODataMcp.Persistence.CatalogStore;
using TheSqlODataMcp.SqlServer;
using Xunit;

namespace TheSqlODataMcp.IntegrationTests;

/// <summary>
/// Composes the full Milestone 1 catalog pipeline against real infrastructure end to end: real SQL Server
/// introspection (<see cref="SqlServerCatalogIntrospector"/>) feeds a real semantic overlay import
/// (<see cref="SemanticOverlayImporter"/>), feeds merge and revision construction
/// (<see cref="CatalogRevisionFactory"/>), feeds persistence into a real file-backed SQLite control store
/// (<see cref="ControlStoreDbContext"/>/<see cref="CatalogRevisionStore"/>) driven through
/// <see cref="CatalogBootstrapCoordinator"/>, feeds deserialization back into a live object graph
/// (<see cref="CatalogRevisionMapper.ToDomain"/>), feeds the in-memory search index
/// (<see cref="CatalogSearchIndex"/>). Every individual stage already has isolated unit/integration
/// coverage elsewhere (see <c>SqlServerCatalogIntrospectorIntegrationTests</c>,
/// <c>SemanticOverlayImporterTests</c>, <c>CatalogMergerTests</c>, <c>CatalogRevisionFactoryTests</c>,
/// <c>CatalogBootstrapCoordinatorTests</c>, <c>CatalogRevisionMapperTests</c>,
/// <c>CatalogSearchIndexTests</c>); this test proves the composition against real SQL Server and a real
/// SQLite database, which none of those isolated tests do.
/// </summary>
public sealed class CatalogPipelineIntegrationTests
{
    private static readonly DateTimeOffset FixedCreatedAt = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private const string CustomersOverlayMarkdown = """
        ---
        catalogVersion: "1.0"
        name: "Reporting catalog overlay"
        entities:
          - source: "crm.Customers"
            displayName: "Flagship Customer Roster"
            description: "Curated top-tier account list used in executive reporting."
            odata:
              enabled: true
              key:
                - "CustomerId"
            fields:
              CustomerCode:
                displayName: "Account Reference"
                description: "External partner-facing account code."
        ---
        # Flagship customer roster overlay

        Administrator narrative for the flagship customer roster overlay.
        """;

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task RealIntrospectionThroughOverlayMergePersistenceAndSearchRoundTripsLosslessly()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        await using var sqlServerFixture = await SqlServerReportingCatalogFixture.CreateAsync(timeout.Token);
        var databasePath = Path.Combine(Path.GetTempPath(), $"thesqlodatamcp-catalog-pipeline-{Guid.NewGuid():N}.db");
        ControlStoreDbContext? dbContext = null;

        try
        {
            await sqlServerFixture.BootstrapAsync(timeout.Token);
            var introspector = new SqlServerCatalogIntrospector(sqlServerFixture.CatalogConnectionString);
            var technicalCatalog = await introspector.IntrospectAsync(timeout.Token);

            var importResult = SemanticOverlayImporter.ImportMarkdownWithFrontMatter(CustomersOverlayMarkdown, technicalCatalog);
            Assert.True(
                importResult.Succeeded,
                FormatDiagnostics("Overlay import failed", importResult.Errors));
            var overlay = importResult.Overlay!;

            var revision = CatalogRevisionFactory.Create(technicalCatalog, overlay, FixedCreatedAt);
            Assert.True(
                revision.Succeeded,
                FormatDiagnostics("Catalog revision failed", revision.Errors));
            var expectedMergedJson = MergedCatalogCanonicalJson.Serialize(revision.MergedCatalog!);

            var options = new DbContextOptionsBuilder<ControlStoreDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            dbContext = new ControlStoreDbContext(options);
            await dbContext.Database.MigrateAsync(timeout.Token);
            var store = new CatalogRevisionStore(dbContext);
            var coordinator = new CatalogBootstrapCoordinator(store);

            var first = await coordinator.RunAsync(CatalogBootstrapMode.ImportIfChanged, technicalCatalog, overlay, FixedCreatedAt, timeout.Token);
            var second = await coordinator.RunAsync(CatalogBootstrapMode.ImportIfChanged, technicalCatalog, overlay, FixedCreatedAt.AddMinutes(1), timeout.Token);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(first!.Id, second!.Id);

            var active = await store.GetActiveAsync(timeout.Token);
            Assert.NotNull(active);
            Assert.Equal(first.Id, active!.Id);

            var roundTrippedRevision = CatalogRevisionMapper.ToDomain(active);
            Assert.True(roundTrippedRevision.Succeeded);
            var actualMergedJson = MergedCatalogCanonicalJson.Serialize(roundTrippedRevision.MergedCatalog!);
            Assert.Equal(expectedMergedJson, actualMergedJson);

            var searchIndex = new CatalogSearchIndex(roundTrippedRevision.MergedCatalog!);
            var matches = searchIndex.Search("Roster");
            Assert.Contains(
                matches,
                match => match.Entity is not null && match.Entity.Physical.Identity.Equals(new PhysicalObjectIdentity("crm", "Customers")));
        }
        finally
        {
            if (dbContext is not null)
            {
                await dbContext.Database.CloseConnectionAsync();
                await dbContext.DisposeAsync();
            }

            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }

            await sqlServerFixture.TeardownAsync(CancellationToken.None);
        }
    }

    private static string FormatDiagnostics(string prefix, IReadOnlyList<SemanticOverlayValidationError> errors) =>
        errors.Count == 0
            ? prefix
            : $"{prefix}: {string.Join("; ", errors.Select(error => $"{error.Code} {error.Path}: {error.Message}"))}";
}
