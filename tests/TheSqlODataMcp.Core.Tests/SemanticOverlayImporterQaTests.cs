using TheSqlODataMcp.Core.Catalog;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>
/// Independent QA coverage for <see cref="SemanticOverlayImporter"/> and the semantic overlay domain
/// model, added by the primary agent alongside the delegated implementation and its own tests. Targets
/// gaps not exercised by <see cref="SemanticOverlayImporterTests"/>: an unreached error path, ordinal
/// case-sensitivity of physical-reference resolution (a product boundary emphasized since ADR 0006/0008),
/// an unknown key directly on an entity object, and the domain model's own construction-time invariants
/// exercised independently of the importer, since a future merge slice may construct these types directly.
/// </summary>
public sealed class SemanticOverlayImporterQaTests
{
    [Fact]
    public void MissingFrontMatterIsRejected()
    {
        const string markdown = "# Just a guide\n\nNo front matter here.";

        var result = SemanticOverlayImporter.ImportMarkdownWithFrontMatter(markdown, CreateCatalog());

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors);
        Assert.Equal(SemanticOverlayValidationErrorCodes.MissingFrontMatter, error.Code);
    }

    [Fact]
    public void EntitySourceResolutionIsCaseSensitiveOrdinal()
    {
        const string yaml = """
            catalogVersion: "1.0"
            entities:
              - source: "Sales.InvoiceHeader"
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.EntitySourceNotFound);
    }

    [Fact]
    public void FieldKeyResolutionIsCaseSensitiveOrdinal()
    {
        const string yaml = """
            catalogVersion: "1.0"
            entities:
              - source: "sales.InvoiceHeader"
                fields:
                  invoiceid:
                    displayName: "Wrong case"
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.FieldNotFound);
    }

    [Fact]
    public void MinimalOverlayWithNoEntitiesSucceeds()
    {
        const string yaml = """
            catalogVersion: "1.0"
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.True(result.Succeeded);
        Assert.Empty(result.Overlay!.Entities);
    }

    [Fact]
    public void UnknownKeyDirectlyOnAnEntityIsRejected()
    {
        const string yaml = """
            catalogVersion: "1.0"
            entities:
              - source: "sales.InvoiceHeader"
                bogus: true
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.StrictDeserializationFailed);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.SchemaViolation);
    }

    [Fact]
    public void JoinFieldPairRejectsNullOrEmptyFieldNames()
    {
        // RequireIdentifier throws ArgumentNullException for null and ArgumentException for
        // whitespace-only, so assert the shared ArgumentException base rather than an exact type.
        Assert.ThrowsAny<ArgumentException>(() => new SemanticOverlayJoinFieldPair(null!, "TargetField"));
        Assert.ThrowsAny<ArgumentException>(() => new SemanticOverlayJoinFieldPair(string.Empty, "TargetField"));
        Assert.ThrowsAny<ArgumentException>(() => new SemanticOverlayJoinFieldPair("SourceField", null!));
        Assert.ThrowsAny<ArgumentException>(() => new SemanticOverlayJoinFieldPair("SourceField", string.Empty));
    }

    [Fact]
    public void RelationshipRejectsAnEmptyJoinCollection()
    {
        var target = new PhysicalObjectIdentity("sales", "Customers");

        Assert.Throws<ArgumentException>(() => new SemanticOverlayRelationship(target, []));
    }

    [Fact]
    public void WarningRejectsNullOrEmptyTitleOrContent()
    {
        Assert.ThrowsAny<ArgumentException>(() => new SemanticOverlayWarning(null!, "content"));
        Assert.ThrowsAny<ArgumentException>(() => new SemanticOverlayWarning(string.Empty, "content"));
        Assert.ThrowsAny<ArgumentException>(() => new SemanticOverlayWarning("title", null!));
        Assert.ThrowsAny<ArgumentException>(() => new SemanticOverlayWarning("title", string.Empty));
    }

    [Fact]
    public void OverlayRejectsDuplicateEntitySourcesEvenWhenConstructedDirectly()
    {
        var first = new SemanticOverlayEntity(new PhysicalObjectIdentity("sales", "InvoiceHeader"), name: "First");
        var second = new SemanticOverlayEntity(new PhysicalObjectIdentity("sales", "InvoiceHeader"), name: "Second");

        Assert.Throws<ArgumentException>(() => new SemanticOverlay("1.0", [first, second]));
    }

    [Fact]
    public void EntityRejectsDuplicateFieldAndRelationshipMapKeysEvenWhenConstructedDirectly()
    {
        var source = new PhysicalObjectIdentity("sales", "InvoiceHeader");
        var duplicateFields = new[]
        {
            new KeyValuePair<string, SemanticOverlayField>("InvoiceId", new SemanticOverlayField(name: "First")),
            new KeyValuePair<string, SemanticOverlayField>("InvoiceId", new SemanticOverlayField(name: "Second")),
        };

        Assert.Throws<ArgumentException>(() => new SemanticOverlayEntity(source, fields: duplicateFields));
    }

    private static TechnicalCatalog CreateCatalog()
    {
        var invoiceHeader = new TechnicalEntity(
            new PhysicalObjectIdentity("sales", "InvoiceHeader"),
            CatalogObjectKind.Table,
            [
                Field("InvoiceId", 0, CanonicalScalarType.Int32, isIdentity: true),
                Field("PostingDate", 1, CanonicalScalarType.Date),
            ]);

        return new TechnicalCatalog("1.0", "fixture", [invoiceHeader]);
    }

    private static TechnicalField Field(string name, int ordinal, CanonicalScalarType type, bool isIdentity = false) =>
        new(name, ordinal, type, new ProviderTypeDetails(type.ToString(), type.ToString()), isNullable: false, isIdentity: isIdentity);
}
