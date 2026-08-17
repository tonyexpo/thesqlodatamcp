using TheSqlODataMcp.Core.Catalog;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

public sealed class SemanticOverlayImporterTests
{
    private const string CombinedMarkdown = """
        ---
        catalogVersion: "1.0"
        name: "Company reporting catalog"
        title: "Catalogo dati aziendale"
        description: "Annotazioni tecniche e descrittive."

        entities:
          - source: "sales.InvoiceHeader"
            name: "Invoices"
            displayName: "Fatture"
            description: "Testate delle fatture emesse."
            aliases:
              - "fatture"
              - "documenti fiscali"

            exposed: true

            odata:
              enabled: true
              entitySetName: "Invoices"
              key:
                - "InvoiceId"

            fields:
              InvoiceId:
                name: "Id"
                displayName: "Identificativo fattura"
                description: "Chiave univoca della fattura."
                aliases:
                  - "numero interno"

              PostingDate:
                displayName: "Data contabile"
                description: "Data di registrazione contabile."

              NetAmount:
                displayName: "Imponibile"
                description: "Importo della fattura al netto dell'IVA."

            relationships:
              customer:
                target: "sales.Customers"
                cardinality: "many-to-one"
                description: "Cliente intestatario della fattura."
                join:
                  - sourceField: "CustomerId"
                    targetField: "CustomerId"

        warnings:
          - title: "Date contabili"
            content: "CreatedAt e' tecnica, non contabile."
        ---
        # Sales reporting guide

        This is opaque administrator-authored narrative content.
        """;

    [Fact]
    public void CombinedMarkdownWithFrontMatterImportsSuccessfully()
    {
        var result = SemanticOverlayImporter.ImportMarkdownWithFrontMatter(CombinedMarkdown, CreateCatalog());

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
        var overlay = result.Overlay!;
        Assert.Equal("1.0", overlay.CatalogVersion);
        Assert.Equal("Company reporting catalog", overlay.Name);
        var entity = Assert.Single(overlay.Entities);
        Assert.Equal("sales.InvoiceHeader", entity.Source.ToString());
        Assert.Equal("Invoices", entity.Name);
        Assert.Equal(["fatture", "documenti fiscali"], entity.Aliases);
        Assert.True(entity.Exposed);
        Assert.Equal("Invoices", entity.OData!.EntitySetName);
        Assert.Equal(["InvoiceId"], entity.OData.Key);
        Assert.Equal("Id", entity.Fields["InvoiceId"].Name);
        Assert.Equal("Data contabile", entity.Fields["PostingDate"].DisplayName);
        var relationship = entity.Relationships["customer"];
        Assert.Equal("sales.Customers", relationship.Target.ToString());
        Assert.Equal(SemanticOverlayCardinality.ManyToOne, relationship.Cardinality);
        Assert.Equal("CustomerId", relationship.Join.Single().SourceField);
        Assert.Equal("CustomerId", relationship.Join.Single().TargetField);
        var warning = Assert.Single(overlay.Warnings);
        Assert.Equal("Date contabili", warning.Title);
        Assert.Contains("opaque administrator-authored narrative content.", overlay.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("catalogVersion", overlay.Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void SeparateYamlAndMarkdownImportsSuccessfully()
    {
        const string yaml = """
            catalogVersion: "1.0"
            entities:
              - source: "sales.Customers"
                name: "Customers"
            """;
        const string markdown = "# Customer guide\n\nSeparate narrative content.";

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, markdown, CreateCatalog());

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
        var overlay = result.Overlay!;
        Assert.Equal("1.0", overlay.CatalogVersion);
        Assert.Equal("Customers", Assert.Single(overlay.Entities).Name);
        Assert.Equal(markdown, overlay.Markdown);
    }

    [Fact]
    public void UnknownTopLevelKeyIsRejected()
    {
        const string yaml = """
            catalogVersion: "1.0"
            typo: true
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.StrictDeserializationFailed);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.SchemaViolation);
    }

    [Fact]
    public void UnknownNestedKeyUnderFieldsIsRejected()
    {
        const string yaml = """
            catalogVersion: "1.0"
            entities:
              - source: "sales.InvoiceHeader"
                fields:
                  InvoiceId:
                    bogus: true
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.StrictDeserializationFailed);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.SchemaViolation);
    }

    [Theory]
    [InlineData("metrics")]
    [InlineData("reports")]
    [InlineData("savedQueries")]
    [InlineData("facts")]
    [InlineData("dimensions")]
    [InlineData("defaultBusinessFilters")]
    public void ForbiddenTopLevelSectionIsRejected(string forbiddenSection)
    {
        var yaml = $"""
            catalogVersion: "1.0"
            {forbiddenSection}:
              - name: "should not be allowed"
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.StrictDeserializationFailed);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.SchemaViolation);
    }

    [Fact]
    public void MissingCatalogVersionIsCaughtByJsonSchemaEvenThoughTypedDeserializationAccepts()
    {
        const string yaml = """
            name: "No version"
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.SchemaViolation);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.CatalogVersionRequired);
        Assert.DoesNotContain(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.StrictDeserializationFailed);
    }

    [Fact]
    public void InvalidCardinalityEnumValueIsCaughtByJsonSchemaEvenThoughTypedDeserializationAccepts()
    {
        const string yaml = """
            catalogVersion: "1.0"
            entities:
              - source: "sales.InvoiceHeader"
                relationships:
                  customer:
                    target: "sales.Customers"
                    cardinality: "sideways"
                    join:
                      - sourceField: "CustomerId"
                        targetField: "CustomerId"
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.SchemaViolation);
        Assert.DoesNotContain(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.StrictDeserializationFailed);
    }

    [Fact]
    public void MalformedYamlSyntaxIsSurfacedAsAValidationErrorNotAnException()
    {
        const string yaml = "catalogVersion: \"1.0\n  entities: [";

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.YamlSyntaxInvalid);
    }

    [Fact]
    public void UnknownEntitySourceIsRejected()
    {
        const string yaml = """
            catalogVersion: "1.0"
            entities:
              - source: "sales.NoSuchTable"
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.EntitySourceNotFound);
        Assert.Equal("$.entities[0].source", error.Path);
    }

    [Fact]
    public void UnknownFieldKeyIsRejected()
    {
        const string yaml = """
            catalogVersion: "1.0"
            entities:
              - source: "sales.InvoiceHeader"
                fields:
                  NoSuchColumn:
                    displayName: "Ghost"
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.FieldNotFound);
    }

    [Fact]
    public void UnknownRelationshipTargetIsRejected()
    {
        const string yaml = """
            catalogVersion: "1.0"
            entities:
              - source: "sales.InvoiceHeader"
                relationships:
                  ghost:
                    target: "sales.NoSuchTable"
                    join:
                      - sourceField: "CustomerId"
                        targetField: "CustomerId"
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.RelationshipTargetNotFound);
    }

    [Fact]
    public void UnknownJoinSourceFieldIsRejected()
    {
        const string yaml = """
            catalogVersion: "1.0"
            entities:
              - source: "sales.InvoiceHeader"
                relationships:
                  customer:
                    target: "sales.Customers"
                    join:
                      - sourceField: "NoSuchColumn"
                        targetField: "CustomerId"
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.JoinSourceFieldNotFound);
    }

    [Fact]
    public void UnknownJoinTargetFieldIsRejected()
    {
        const string yaml = """
            catalogVersion: "1.0"
            entities:
              - source: "sales.InvoiceHeader"
                relationships:
                  customer:
                    target: "sales.Customers"
                    join:
                      - sourceField: "CustomerId"
                        targetField: "NoSuchColumn"
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.JoinTargetFieldNotFound);
    }

    [Fact]
    public void DuplicateEntitySourceIsRejected()
    {
        const string yaml = """
            catalogVersion: "1.0"
            entities:
              - source: "sales.InvoiceHeader"
                name: "First"
              - source: "sales.InvoiceHeader"
                name: "Second"
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.EntitySourceDuplicate);
    }

    [Fact]
    public void MultipleSimultaneousProblemsAreAllCollectedInOnePass()
    {
        const string yaml = """
            catalogVersion: "1.0"
            entities:
              - source: "sales.InvoiceHeader"
                fields:
                  NoSuchColumn:
                    displayName: "Ghost"
                relationships:
                  ghost:
                    target: "sales.NoSuchTable"
                    join:
                      - sourceField: "NoSuchColumn"
                        targetField: "AlsoMissing"
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, CreateCatalog());

        Assert.False(result.Succeeded);
        Assert.True(result.Errors.Count >= 3, $"Expected at least 3 errors, found {result.Errors.Count}.");
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.FieldNotFound);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.RelationshipTargetNotFound);
        Assert.Contains(result.Errors, e => e.Code == SemanticOverlayValidationErrorCodes.JoinSourceFieldNotFound);
    }

    [Fact]
    public void NullArgumentsAreRejected()
    {
        var catalog = CreateCatalog();

        Assert.Throws<ArgumentNullException>(() => SemanticOverlayImporter.ImportMarkdownWithFrontMatter(null!, catalog));
        Assert.Throws<ArgumentNullException>(() => SemanticOverlayImporter.ImportMarkdownWithFrontMatter(CombinedMarkdown, null!));
        Assert.Throws<ArgumentNullException>(() => SemanticOverlayImporter.ImportYamlAndMarkdown(null!, string.Empty, catalog));
        Assert.Throws<ArgumentNullException>(() => SemanticOverlayImporter.ImportYamlAndMarkdown("catalogVersion: \"1.0\"", null!, catalog));
        Assert.Throws<ArgumentNullException>(() => SemanticOverlayImporter.ImportYamlAndMarkdown("catalogVersion: \"1.0\"", string.Empty, null!));
    }

    [Fact]
    public void ImportingIdenticalInputTwiceIsDeterministic()
    {
        var catalog = CreateCatalog();

        var first = SemanticOverlayImporter.ImportMarkdownWithFrontMatter(CombinedMarkdown, catalog);
        var second = SemanticOverlayImporter.ImportMarkdownWithFrontMatter(CombinedMarkdown, catalog);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        AssertOverlaysEquivalent(first.Overlay!, second.Overlay!);
    }

    private static void AssertOverlaysEquivalent(SemanticOverlay a, SemanticOverlay b)
    {
        Assert.Equal(a.CatalogVersion, b.CatalogVersion);
        Assert.Equal(a.Name, b.Name);
        Assert.Equal(a.Title, b.Title);
        Assert.Equal(a.Description, b.Description);
        Assert.Equal(a.Markdown, b.Markdown);
        Assert.Equal(a.Warnings.Select(w => (w.Title, w.Content)), b.Warnings.Select(w => (w.Title, w.Content)));
        Assert.Equal(a.Entities.Select(e => e.Source.ToString()), b.Entities.Select(e => e.Source.ToString()));

        foreach (var entityA in a.Entities)
        {
            var entityB = b.Entities.Single(e => e.Source.Equals(entityA.Source));
            Assert.Equal(entityA.Name, entityB.Name);
            Assert.Equal(entityA.DisplayName, entityB.DisplayName);
            Assert.Equal(entityA.Aliases, entityB.Aliases);
            Assert.Equal(entityA.Exposed, entityB.Exposed);
            Assert.Equal(entityA.Fields.Keys.OrderBy(k => k, StringComparer.Ordinal), entityB.Fields.Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.Equal(entityA.Relationships.Keys.OrderBy(k => k, StringComparer.Ordinal), entityB.Relationships.Keys.OrderBy(k => k, StringComparer.Ordinal));
            foreach (var (name, relationshipA) in entityA.Relationships)
            {
                var relationshipB = entityB.Relationships[name];
                Assert.Equal(relationshipA.Target.ToString(), relationshipB.Target.ToString());
                Assert.Equal(relationshipA.Cardinality, relationshipB.Cardinality);
                Assert.Equal(
                    relationshipA.Join.Select(p => (p.SourceField, p.TargetField)),
                    relationshipB.Join.Select(p => (p.SourceField, p.TargetField)));
            }
        }
    }

    private static TechnicalCatalog CreateCatalog()
    {
        var invoiceHeader = new TechnicalEntity(
            new PhysicalObjectIdentity("sales", "InvoiceHeader"),
            CatalogObjectKind.Table,
            [
                Field("InvoiceId", 0, CanonicalScalarType.Int32, isIdentity: true),
                Field("PostingDate", 1, CanonicalScalarType.Date),
                Field("NetAmount", 2, CanonicalScalarType.Decimal),
                Field("CustomerId", 3, CanonicalScalarType.Int32),
            ]);
        var customers = new TechnicalEntity(
            new PhysicalObjectIdentity("sales", "Customers"),
            CatalogObjectKind.Table,
            [
                Field("CustomerId", 0, CanonicalScalarType.Int32, isIdentity: true),
                Field("Name", 1, CanonicalScalarType.String),
            ]);

        return new TechnicalCatalog("1.0", "fixture", [invoiceHeader, customers]);
    }

    private static TechnicalField Field(string name, int ordinal, CanonicalScalarType type, bool isIdentity = false) =>
        new(name, ordinal, type, new ProviderTypeDetails(type.ToString(), type.ToString()), isNullable: false, isIdentity: isIdentity);
}
