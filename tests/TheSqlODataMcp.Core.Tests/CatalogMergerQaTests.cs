using TheSqlODataMcp.Core.Catalog;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>
/// Independent QA coverage for <see cref="CatalogMerger"/>, added by the primary agent after reviewing the
/// delegated implementation. Targets a real defect found during review: whitespace-only overlay strings
/// reached <see cref="TechnicalCatalog.RequireIdentifier"/> deep inside <see cref="MergedEntity"/>/
/// <see cref="MergedRelationship"/> construction and threw an unhandled <see cref="ArgumentException"/>
/// instead of surfacing as a <see cref="CatalogMergeResult"/> failure — violating this slice's own design
/// principle that malformed overlay content must never escape as an exception from the public API. Slice
/// 4A's schema does not forbid whitespace-only <c>displayName</c>/<c>name</c> values or whitespace-only
/// YAML relationship map keys, since it validates property values, not property names, so this is reachable
/// from real (if unusual) overlay input, not just from directly-constructed test doubles.
/// </summary>
public sealed class CatalogMergerQaTests
{
    [Fact]
    public void WhitespaceOnlyDisplayNameFallsBackToPhysicalObjectNameInsteadOfCrashing()
    {
        var catalog = CreateCatalog();
        var overlay = new SemanticOverlay(
            "1.0",
            [new SemanticOverlayEntity(new PhysicalObjectIdentity("sales", "InvoiceHeader"), displayName: "   ")]);

        var result = CatalogMerger.Merge(catalog, overlay);

        Assert.True(result.Succeeded);
        Assert.Equal(
            "InvoiceHeader",
            result.MergedCatalog!.Entities.Single(e => e.Physical.Identity.ObjectName == "InvoiceHeader").DisplayName);
    }

    [Fact]
    public void WhitespaceOnlyDisplayNameFallsBackToNameWhenNameIsUsable()
    {
        var catalog = CreateCatalog();
        var overlay = new SemanticOverlay(
            "1.0",
            [new SemanticOverlayEntity(new PhysicalObjectIdentity("sales", "InvoiceHeader"), name: "Invoices", displayName: "  ")]);

        var result = CatalogMerger.Merge(catalog, overlay);

        Assert.True(result.Succeeded);
        Assert.Equal(
            "Invoices",
            result.MergedCatalog!.Entities.Single(e => e.Physical.Identity.ObjectName == "InvoiceHeader").DisplayName);
    }

    [Fact]
    public void WhitespaceOnlyDisplayNameAndNameBothFallBackToPhysicalObjectName()
    {
        var catalog = CreateCatalog();
        var overlay = new SemanticOverlay(
            "1.0",
            [new SemanticOverlayEntity(new PhysicalObjectIdentity("sales", "InvoiceHeader"), name: " ", displayName: "\t")]);

        var result = CatalogMerger.Merge(catalog, overlay);

        Assert.True(result.Succeeded);
        Assert.Equal(
            "InvoiceHeader",
            result.MergedCatalog!.Entities.Single(e => e.Physical.Identity.ObjectName == "InvoiceHeader").DisplayName);
    }

    [Fact]
    public void WhitespaceOnlyRelationshipNameIsRejectedRatherThanCrashing()
    {
        var catalog = CreateCatalog();
        var overlay = new SemanticOverlay(
            "1.0",
            [
                new SemanticOverlayEntity(
                    new PhysicalObjectIdentity("sales", "InvoiceHeader"),
                    relationships:
                    [
                        new KeyValuePair<string, SemanticOverlayRelationship>(
                            "  ",
                            new SemanticOverlayRelationship(
                                new PhysicalObjectIdentity("sales", "Customers"),
                                [new SemanticOverlayJoinFieldPair("CustomerId", "CustomerId")])),
                    ]),
            ]);

        var result = CatalogMerger.Merge(catalog, overlay);

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors);
        Assert.Equal(CatalogMergeErrorCodes.RelationshipNameInvalid, error.Code);
    }

    private static TechnicalCatalog CreateCatalog()
    {
        var invoiceHeader = new TechnicalEntity(
            new PhysicalObjectIdentity("sales", "InvoiceHeader"),
            CatalogObjectKind.Table,
            [
                Field("InvoiceId", 0, CanonicalScalarType.Int32, isIdentity: true),
                Field("CustomerId", 1, CanonicalScalarType.Int32),
            ],
            [new CatalogKey("PK_InvoiceHeader", ["InvoiceId"], isPrimary: true)]);

        var customers = new TechnicalEntity(
            new PhysicalObjectIdentity("sales", "Customers"),
            CatalogObjectKind.Table,
            [
                Field("CustomerId", 0, CanonicalScalarType.Int32, isIdentity: true),
                Field("Name", 1, CanonicalScalarType.String),
            ],
            [new CatalogKey("PK_Customers", ["CustomerId"], isPrimary: true)]);

        return new TechnicalCatalog("1.0", "fixture", [invoiceHeader, customers]);
    }

    private static TechnicalField Field(string name, int ordinal, CanonicalScalarType type, bool isIdentity = false) =>
        new(name, ordinal, type, new ProviderTypeDetails(type.ToString(), type.ToString()), isNullable: false, isIdentity: isIdentity);
}
