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
/// SQLite database, which none of those isolated tests do. Beyond the base round trip, it also exercises,
/// through that same real pipeline: an overlay-configured (non-FK) relationship
/// (<c>sales.Invoices</c> &#8594; <c>archive.Invoices</c>, joined on <c>InvoiceNumber</c>, which has no
/// physical foreign key); non-ASCII overlay text surviving the real SQLite round trip byte for byte; an
/// overlay entry on the genuinely keyless <c>reporting.InvoiceDetail</c> view, asserting it still resolves
/// to an empty <see cref="MergedEntity.EffectiveKeyFields"/>; and a failed-then-succeeded
/// <see cref="CatalogBootstrapCoordinator.RunAsync"/> sequence (a merge-time catalog-version mismatch) run
/// against the real SQL-Server-introspected <see cref="TechnicalCatalog"/>, proving the previously active
/// revision survives the failure untouched and a subsequent valid rebuild recovers.
/// </summary>
public sealed class CatalogPipelineIntegrationTests
{
    private static readonly DateTimeOffset FixedCreatedAt = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private const string ArchivedInvoiceRelationshipDescription =
        "Copia archiviata della fattura, così collegata tramite il numero fattura — 🔗 dati storici non vincolati da SQL.";

    private static readonly string CustomersOverlayMarkdown = $$"""
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
          - source: "sales.Invoices"
            displayName: "Sales Invoices"
            relationships:
              archivedCopy:
                target: "archive.Invoices"
                cardinality: "one-to-one"
                description: "{{ArchivedInvoiceRelationshipDescription}}"
                join:
                  - sourceField: "InvoiceNumber"
                    targetField: "InvoiceNumber"
          - source: "reporting.InvoiceDetail"
            displayName: "Invoice Detail (keyless view)"
            description: "Vista di reporting senza chiave primaria dichiarata; unisce fattura, cliente e riga prodotto."
        ---
        # Flagship customer roster overlay

        Administrator narrative for the flagship customer roster overlay.
        """;

    private const string MismatchedCatalogVersionOverlayMarkdown = """
        ---
        catalogVersion: "9.9"
        name: "Mismatched catalog version overlay"
        ---
        # Mismatched catalog version overlay

        Exercises the merge-time catalog version mismatch guard (ADR 0011) against the real
        SQL-Server-introspected catalog.
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

            var mergedCatalog = roundTrippedRevision.MergedCatalog!;

            // Gap 1 (overlay-configured, non-FK relationship) + gap 2 (non-ASCII overlay text): sales.Invoices
            // and archive.Invoices are not linked by any physical foreign key, so this relationship only
            // exists because the overlay declared it, and its description carries non-ASCII text that must
            // have survived the real SQLite round trip byte for byte.
            var invoicesEntity = mergedCatalog.Entities.Single(
                entity => entity.Physical.Identity.Equals(new PhysicalObjectIdentity("sales", "Invoices")));
            var archivedCopyRelationship = invoicesEntity.Relationships.Single(
                relationship => relationship.Provenance == RelationshipProvenance.Configured);
            Assert.Equal(new PhysicalObjectIdentity("archive", "Invoices"), archivedCopyRelationship.Target);
            Assert.Equal(SemanticOverlayCardinality.OneToOne, archivedCopyRelationship.Cardinality);
            Assert.Contains(
                archivedCopyRelationship.FieldPairs,
                pair => string.Equals(pair.SourceField, "InvoiceNumber", StringComparison.Ordinal)
                    && string.Equals(pair.TargetField, "InvoiceNumber", StringComparison.Ordinal));
            Assert.Equal(ArchivedInvoiceRelationshipDescription, archivedCopyRelationship.Description);

            // Gap 3 (keyless overlay entity): reporting.InvoiceDetail is a real keyless view; an overlay
            // entry that declares no odata.key must still resolve to an empty effective key after the full
            // round trip, not silently invent one.
            var invoiceDetailEntity = mergedCatalog.Entities.Single(
                entity => entity.Physical.Identity.Equals(new PhysicalObjectIdentity("reporting", "InvoiceDetail")));
            Assert.Equal("Invoice Detail (keyless view)", invoiceDetailEntity.DisplayName);
            Assert.Empty(invoiceDetailEntity.EffectiveKeyFields);

            var searchIndex = new CatalogSearchIndex(mergedCatalog);
            var matches = searchIndex.Search("Roster");
            Assert.Contains(
                matches,
                match => match.Entity is not null && match.Entity.Physical.Identity.Equals(new PhysicalObjectIdentity("crm", "Customers")));

            // Gap 2, continued: the non-ASCII relationship description is also reachable through the search
            // index built off the round-tripped catalog, not just via direct property equality.
            var nonAsciiMatches = searchIndex.Search("così collegata");
            Assert.Contains(
                nonAsciiMatches,
                match => match.Relationship == archivedCopyRelationship
                    && string.Equals(match.MatchedText, ArchivedInvoiceRelationshipDescription, StringComparison.Ordinal));

            // Gap 4 (failed-then-succeeded bootstrap against the real introspected catalog): a merge-time
            // catalog-version mismatch must fail the rebuild while leaving the previously active revision
            // (captured above as `active`) untouched, and a subsequent valid rebuild must recover.
            var mismatchedImportResult = SemanticOverlayImporter.ImportMarkdownWithFrontMatter(MismatchedCatalogVersionOverlayMarkdown, technicalCatalog);
            Assert.True(
                mismatchedImportResult.Succeeded,
                FormatDiagnostics("Mismatched-version overlay import unexpectedly failed", mismatchedImportResult.Errors));
            var mismatchedOverlay = mismatchedImportResult.Overlay!;

            var failedRebuildResult = await coordinator.RunAsync(
                CatalogBootstrapMode.AlwaysImport, technicalCatalog, mismatchedOverlay, FixedCreatedAt.AddMinutes(2), timeout.Token);
            Assert.NotNull(failedRebuildResult);
            Assert.Equal(active.Id, failedRebuildResult!.Id);
            Assert.Equal(active.ActivatedAt, failedRebuildResult.ActivatedAt);

            var allRevisionsAfterFailure = await dbContext.CatalogRevisions.AsNoTracking().ToListAsync(timeout.Token);
            var failedRevision = allRevisionsAfterFailure.Single(revision => revision.CreatedAt == FixedCreatedAt.AddMinutes(2));
            Assert.False(failedRevision.Succeeded);
            Assert.Equal(CatalogRevisionStatus.Failed, failedRevision.Status);
            Assert.Null(failedRevision.ActivatedAt);

            var activeAfterFailure = await store.GetActiveAsync(timeout.Token);
            Assert.Equal(active.Id, activeAfterFailure!.Id);

            var recovered = await coordinator.RunAsync(
                CatalogBootstrapMode.AlwaysImport, technicalCatalog, overlay, FixedCreatedAt.AddMinutes(3), timeout.Token);
            Assert.NotNull(recovered);
            Assert.NotEqual(active.Id, recovered!.Id);
            Assert.Equal(FixedCreatedAt.AddMinutes(3), recovered.ActivatedAt);

            var recoveredDomain = CatalogRevisionMapper.ToDomain(recovered);
            Assert.True(recoveredDomain.Succeeded);
            Assert.Equal(expectedMergedJson, MergedCatalogCanonicalJson.Serialize(recoveredDomain.MergedCatalog!));

            var activeAfterRecovery = await store.GetActiveAsync(timeout.Token);
            Assert.Equal(recovered.Id, activeAfterRecovery!.Id);
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
