using TheSqlODataMcp.Core.Catalog;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>
/// Construction-time invariant tests for the merged-catalog domain model
/// (<see cref="MergedCatalog"/>, <see cref="MergedEntity"/>, <see cref="MergedField"/>,
/// <see cref="MergedRelationship"/>, <see cref="CatalogMergeResult"/>), constructed directly rather than
/// only through <see cref="CatalogMerger"/>, plus determinism/sensitivity coverage for
/// <see cref="MergedCatalogCanonicalJson"/>.
/// </summary>
public sealed class MergedCatalogTests
{
    [Fact]
    public void MergedFieldRequiresNonNullPhysical()
    {
        Assert.Throws<ArgumentNullException>(() => new MergedField(null!));
    }

    [Fact]
    public void MergedEntityRequiresNonNullPhysicalAndNonEmptyDisplayName()
    {
        var physical = CreateInvoiceHeaderEntity();

        Assert.Throws<ArgumentNullException>(() => new MergedEntity(null!, "Invoices", []));
        Assert.ThrowsAny<ArgumentException>(() => new MergedEntity(physical, string.Empty, []));
        Assert.ThrowsAny<ArgumentException>(() => new MergedEntity(physical, null!, []));
    }

    [Fact]
    public void MergedEntityRejectsFieldsNotPresentOnThePhysicalEntity()
    {
        var physical = CreateInvoiceHeaderEntity();
        var bogusField = new MergedField(Field("Bogus", 0, CanonicalScalarType.Int32));

        Assert.Throws<ArgumentException>(() => new MergedEntity(physical, "Invoices", [bogusField]));
    }

    [Fact]
    public void MergedEntityRejectsEffectiveKeyFieldsNotPresentOnThePhysicalEntity()
    {
        var physical = CreateInvoiceHeaderEntity();

        Assert.Throws<ArgumentException>(() => new MergedEntity(physical, "Invoices", [], effectiveKeyFields: ["Bogus"]));
    }

    [Fact]
    public void MergedEntityRejectsRelationshipSourceFieldsNotPresentOnThePhysicalEntity()
    {
        var physical = CreateInvoiceHeaderEntity();
        var badRelationship = new MergedRelationship(
            "ghost",
            new PhysicalObjectIdentity("sales", "Customers"),
            [new RelationshipFieldPair("Bogus", "CustomerId")],
            RelationshipProvenance.Discovered);

        Assert.Throws<ArgumentException>(() => new MergedEntity(physical, "Invoices", [], relationships: [badRelationship]));
    }

    [Fact]
    public void MergedEntityRejectsDuplicateFieldNamesEvenWhenConstructedDirectly()
    {
        var physical = CreateInvoiceHeaderEntity();
        var invoiceIdField = physical.Fields.Single(f => f.Name == "InvoiceId");
        var duplicateFields = new[] { new MergedField(invoiceIdField), new MergedField(invoiceIdField) };

        Assert.Throws<ArgumentException>(() => new MergedEntity(physical, "Invoices", duplicateFields));
    }

    [Fact]
    public void MergedEntityRejectsDuplicateRelationshipNameAndProvenancePairs()
    {
        var physical = CreateInvoiceHeaderEntity();
        var relationship = new MergedRelationship(
            "customer",
            new PhysicalObjectIdentity("sales", "Customers"),
            [new RelationshipFieldPair("CustomerId", "CustomerId")],
            RelationshipProvenance.Discovered);
        var duplicate = new MergedRelationship(
            "customer",
            new PhysicalObjectIdentity("sales", "Customers"),
            [new RelationshipFieldPair("CustomerId", "CustomerId")],
            RelationshipProvenance.Discovered);

        Assert.Throws<ArgumentException>(() => new MergedEntity(physical, "Invoices", [], relationships: [relationship, duplicate]));
    }

    [Fact]
    public void MergedEntitySameNameIsAllowedForDifferentProvenance()
    {
        var physical = CreateInvoiceHeaderEntity();
        var discovered = new MergedRelationship(
            "customer",
            new PhysicalObjectIdentity("sales", "Customers"),
            [new RelationshipFieldPair("CustomerId", "CustomerId")],
            RelationshipProvenance.Discovered);
        var configured = new MergedRelationship(
            "customer",
            new PhysicalObjectIdentity("sales", "Customers"),
            [new RelationshipFieldPair("CustomerId", "CustomerId")],
            RelationshipProvenance.Configured);

        var entity = new MergedEntity(physical, "Invoices", [], relationships: [discovered, configured]);

        Assert.Equal(2, entity.Relationships.Count);
    }

    [Fact]
    public void MergedRelationshipConstructionRejectsInvalidInput()
    {
        var target = new PhysicalObjectIdentity("sales", "Customers");
        var pair = new RelationshipFieldPair("CustomerId", "CustomerId");

        Assert.Throws<ArgumentNullException>(() => new MergedRelationship("r", null!, [pair], RelationshipProvenance.Discovered));
        Assert.Throws<ArgumentException>(() => new MergedRelationship("r", target, [], RelationshipProvenance.Discovered));
        Assert.Throws<ArgumentException>(() => new MergedRelationship("r", target, [pair, pair], RelationshipProvenance.Discovered));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MergedRelationship("r", target, [pair], (RelationshipProvenance)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MergedRelationship(
            "r", target, [pair], RelationshipProvenance.Configured, cardinality: (SemanticOverlayCardinality)int.MaxValue));
    }

    [Fact]
    public void MergedCatalogConstructionRejectsInvalidInput()
    {
        var entity = new MergedEntity(CreateInvoiceHeaderEntity(), "Invoices", []);
        var duplicateEntity = new MergedEntity(CreateInvoiceHeaderEntity(), "Invoices again", []);

        Assert.Throws<ArgumentException>(() => new MergedCatalog(" ", "fixture", configured: false, [entity]));
        Assert.Throws<ArgumentException>(() => new MergedCatalog("1.0", " ", configured: false, [entity]));
        Assert.Throws<ArgumentException>(() => new MergedCatalog("1.0", "fixture", configured: false, [entity, duplicateEntity]));
        Assert.Throws<ArgumentException>(() => new MergedCatalog("1.0", "fixture", configured: false, [entity], warnings: [null!]));
    }

    [Fact]
    public void CatalogMergeResultFailureRejectsZeroErrorsAndSuccessRejectsNull()
    {
        Assert.Throws<ArgumentException>(() => CatalogMergeResult.Failure([]));
        Assert.Throws<ArgumentNullException>(() => CatalogMergeResult.Success(null!));
    }

    [Fact]
    public void CanonicalJsonIsDeterministicAndRowOrderIndependentAcrossRelationshipUnion()
    {
        var invoice = CreateInvoiceHeaderEntity();
        var customer = CreateCustomersEntity();

        var discovered = new MergedRelationship(
            "FK_Invoices_Customer", new PhysicalObjectIdentity("sales", "Customers"),
            [new RelationshipFieldPair("CustomerId", "CustomerId")], RelationshipProvenance.Discovered, description: "FK");
        var configured = new MergedRelationship(
            "customer", new PhysicalObjectIdentity("sales", "Customers"),
            [new RelationshipFieldPair("CustomerId", "CustomerId")], RelationshipProvenance.Configured,
            SemanticOverlayCardinality.ManyToOne, "Configured customer link");

        var invoiceEntityA = new MergedEntity(
            invoice, "Invoices", AllFieldsAsMerged(invoice), ["fatture"], "Fatture", true,
            new SemanticOverlayODataSettings(true, "Invoices", ["InvoiceId"]),
            ["InvoiceId"], [discovered, configured]);
        var invoiceEntityB = new MergedEntity(
            invoice, "Invoices", AllFieldsAsMerged(invoice), ["fatture"], "Fatture", true,
            new SemanticOverlayODataSettings(true, "Invoices", ["InvoiceId"]),
            ["InvoiceId"], [configured, discovered]);
        var customerEntity = new MergedEntity(customer, "Customers", AllFieldsAsMerged(customer));

        var first = new MergedCatalog("1.0", "fixture", true, [invoiceEntityA, customerEntity], "cat", "Catalog", "Desc", markdown: "# doc");
        var second = new MergedCatalog("1.0", "fixture", true, [customerEntity, invoiceEntityB], "cat", "Catalog", "Desc", markdown: "# doc");

        var firstJson = MergedCatalogCanonicalJson.Serialize(first);
        var secondJson = MergedCatalogCanonicalJson.Serialize(second);

        Assert.Equal(firstJson, secondJson);
        Assert.Equal(MergedCatalogCanonicalJson.ComputeStructuralHash(first), MergedCatalogCanonicalJson.ComputeStructuralHash(second));
    }

    [Fact]
    public void CanonicalHashHasExpectedShape()
    {
        var catalog = SimpleCatalog();
        var hash = MergedCatalogCanonicalJson.ComputeStructuralHash(catalog);

        Assert.Equal(64, hash.Length);
        Assert.All(hash, character => Assert.True(char.IsAsciiDigit(character) || character is >= 'a' and <= 'f'));
    }

    [Fact]
    public void CanonicalHashChangesWhenDisplayNameChanges()
    {
        var baseline = SimpleCatalog();
        var changed = SimpleCatalog(displayName: "Different display name");

        Assert.NotEqual(MergedCatalogCanonicalJson.ComputeStructuralHash(baseline), MergedCatalogCanonicalJson.ComputeStructuralHash(changed));
    }

    [Fact]
    public void CanonicalHashChangesWhenAliasesChange()
    {
        var baseline = SimpleCatalog();
        var changed = SimpleCatalog(aliases: ["extra alias"]);

        Assert.NotEqual(MergedCatalogCanonicalJson.ComputeStructuralHash(baseline), MergedCatalogCanonicalJson.ComputeStructuralHash(changed));
    }

    [Fact]
    public void CanonicalHashChangesWhenExposedFlagChanges()
    {
        var baseline = SimpleCatalog(exposed: true);
        var changed = SimpleCatalog(exposed: false);

        Assert.NotEqual(MergedCatalogCanonicalJson.ComputeStructuralHash(baseline), MergedCatalogCanonicalJson.ComputeStructuralHash(changed));
    }

    [Fact]
    public void CanonicalHashChangesWhenARelationshipIsAdded()
    {
        var baseline = SimpleCatalog();
        var changed = SimpleCatalog(withConfiguredRelationship: true);

        Assert.NotEqual(MergedCatalogCanonicalJson.ComputeStructuralHash(baseline), MergedCatalogCanonicalJson.ComputeStructuralHash(changed));
    }

    [Fact]
    public void CanonicalHashChangesWhenEffectiveKeyFieldsChange()
    {
        var baseline = SimpleCatalog(effectiveKeyFields: ["InvoiceId"]);
        var changed = SimpleCatalog(effectiveKeyFields: ["CustomerId"]);

        Assert.NotEqual(MergedCatalogCanonicalJson.ComputeStructuralHash(baseline), MergedCatalogCanonicalJson.ComputeStructuralHash(changed));
    }

    private static MergedCatalog SimpleCatalog(
        string displayName = "Invoices",
        IEnumerable<string>? aliases = null,
        bool exposed = true,
        bool withConfiguredRelationship = false,
        IEnumerable<string>? effectiveKeyFields = null)
    {
        var physical = CreateInvoiceHeaderEntity();
        var relationships = new List<MergedRelationship>
        {
            new(
                "FK_Invoices_Customer",
                new PhysicalObjectIdentity("sales", "Customers"),
                [new RelationshipFieldPair("CustomerId", "CustomerId")],
                RelationshipProvenance.Discovered),
        };
        if (withConfiguredRelationship)
        {
            relationships.Add(new MergedRelationship(
                "customer",
                new PhysicalObjectIdentity("sales", "Customers"),
                [new RelationshipFieldPair("CustomerId", "CustomerId")],
                RelationshipProvenance.Configured,
                SemanticOverlayCardinality.ManyToOne));
        }

        var entity = new MergedEntity(
            physical,
            displayName,
            AllFieldsAsMerged(physical),
            aliases ?? ["fatture"],
            "Fatture",
            exposed,
            new SemanticOverlayODataSettings(true, "Invoices", ["InvoiceId"]),
            effectiveKeyFields ?? ["InvoiceId"],
            relationships);

        return new MergedCatalog("1.0", "fixture", true, [entity]);
    }

    private static IEnumerable<MergedField> AllFieldsAsMerged(TechnicalEntity entity) =>
        entity.Fields.Select(field => new MergedField(field));

    private static TechnicalEntity CreateInvoiceHeaderEntity() =>
        new(
            new PhysicalObjectIdentity("sales", "InvoiceHeader"),
            CatalogObjectKind.Table,
            [
                Field("InvoiceId", 0, CanonicalScalarType.Int32, isIdentity: true),
                Field("PostingDate", 1, CanonicalScalarType.Date),
                Field("NetAmount", 2, CanonicalScalarType.Decimal),
                Field("CustomerId", 3, CanonicalScalarType.Int32),
            ],
            [new CatalogKey("PK_InvoiceHeader", ["InvoiceId"], isPrimary: true)],
            relationships:
            [
                new CatalogRelationship(
                    "FK_Invoices_Customer",
                    new PhysicalObjectIdentity("sales", "Customers"),
                    [new RelationshipFieldPair("CustomerId", "CustomerId")]),
            ],
            description: "Invoice headers");

    private static TechnicalEntity CreateCustomersEntity() =>
        new(
            new PhysicalObjectIdentity("sales", "Customers"),
            CatalogObjectKind.Table,
            [
                Field("CustomerId", 0, CanonicalScalarType.Int32, isIdentity: true),
                Field("Name", 1, CanonicalScalarType.String),
            ],
            [new CatalogKey("PK_Customers", ["CustomerId"], isPrimary: true)]);

    private static TechnicalField Field(string name, int ordinal, CanonicalScalarType type, bool isIdentity = false) =>
        new(name, ordinal, type, new ProviderTypeDetails(type.ToString(), type.ToString()), isNullable: false, isIdentity: isIdentity);
}
