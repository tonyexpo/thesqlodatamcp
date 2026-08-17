using TheSqlODataMcp.Core.Catalog;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

public sealed class CatalogMergerTests
{
    [Fact]
    public void NullCatalogIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => CatalogMerger.Merge(null!, null));
    }

    [Fact]
    public void MergingWithNoOverlaySucceedsAndFallsBackToPhysicalOnlyValuesForEveryEntity()
    {
        var catalog = CreateCatalog();

        var result = CatalogMerger.Merge(catalog, null);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
        var merged = result.MergedCatalog!;
        Assert.False(merged.Configured);
        Assert.Null(merged.Name);
        Assert.Null(merged.Title);
        Assert.Null(merged.Description);
        Assert.Null(merged.Markdown);
        Assert.Empty(merged.Warnings);
        Assert.Equal(catalog.Entities.Count, merged.Entities.Count);

        var invoiceEntity = merged.Entities.Single(e => e.Physical.Identity.ObjectName == "InvoiceHeader");
        Assert.Equal("InvoiceHeader", invoiceEntity.DisplayName);
        Assert.Equal("Invoice headers", invoiceEntity.Description);
        Assert.Empty(invoiceEntity.Aliases);
        Assert.True(invoiceEntity.Exposed);
        Assert.Null(invoiceEntity.OData);
        Assert.Equal(["InvoiceId"], invoiceEntity.EffectiveKeyFields);
        var discoveredOnly = Assert.Single(invoiceEntity.Relationships);
        Assert.Equal(RelationshipProvenance.Discovered, discoveredOnly.Provenance);
        Assert.Equal("FK_Invoices_Customer", discoveredOnly.Name);
        Assert.Null(discoveredOnly.Cardinality);

        var keylessEntity = merged.Entities.Single(e => e.Physical.Identity.ObjectName == "KeylessSummary");
        Assert.Empty(keylessEntity.EffectiveKeyFields);
        Assert.Equal("KeylessSummary", keylessEntity.DisplayName);
    }

    [Fact]
    public void FullMergeLayersOverlayDataAndPhysicalDescriptionShowsThroughWhenOverlayOmitsIt()
    {
        var catalog = CreateCatalog();
        var invoiceOverlay = new SemanticOverlayEntity(
            new PhysicalObjectIdentity("sales", "InvoiceHeader"),
            name: "Invoices",
            displayName: "Fatture",
            description: "Le fatture emesse.",
            aliases: ["fatture", "documenti"],
            exposed: false,
            odata: new SemanticOverlayODataSettings(true, "Invoices", ["InvoiceId"]),
            fields:
            [
                new KeyValuePair<string, SemanticOverlayField>(
                    "NetAmount",
                    new SemanticOverlayField(displayName: "Imponibile", description: "Importo netto")),
            ]);
        // No displayName/name/description override: exercises the physical-metadata-shows-through path.
        var customersOverlay = new SemanticOverlayEntity(new PhysicalObjectIdentity("sales", "Customers"));
        var overlay = new SemanticOverlay("1.0", [invoiceOverlay, customersOverlay]);

        var result = CatalogMerger.Merge(catalog, overlay);

        Assert.True(result.Succeeded);
        var merged = result.MergedCatalog!;
        Assert.True(merged.Configured);

        var invoiceEntity = merged.Entities.Single(e => e.Physical.Identity.ObjectName == "InvoiceHeader");
        Assert.Equal("Fatture", invoiceEntity.DisplayName);
        Assert.Equal("Le fatture emesse.", invoiceEntity.Description);
        Assert.Equal(["fatture", "documenti"], invoiceEntity.Aliases);
        Assert.False(invoiceEntity.Exposed);
        Assert.Equal("Invoices", invoiceEntity.OData!.EntitySetName);
        var netAmountField = invoiceEntity.Fields.Single(f => f.Physical.Name == "NetAmount");
        Assert.Equal("Imponibile", netAmountField.DisplayName);
        Assert.Equal("Importo netto", netAmountField.Description);
        var postingDateField = invoiceEntity.Fields.Single(f => f.Physical.Name == "PostingDate");
        Assert.Null(postingDateField.DisplayName);
        Assert.Null(postingDateField.Description);

        var customerEntity = merged.Entities.Single(e => e.Physical.Identity.ObjectName == "Customers");
        Assert.Equal("Customers", customerEntity.DisplayName);
        Assert.Equal("Customer master data", customerEntity.Description);
        Assert.Empty(customerEntity.Aliases);
        Assert.True(customerEntity.Exposed);
        Assert.Null(customerEntity.OData);
    }

    [Fact]
    public void AnEntityWithNoMatchingOverlayEntityFallsBackToPhysicalOnlyValues()
    {
        var catalog = CreateCatalog();
        // Overlay covers only InvoiceHeader; Customers and KeylessSummary are intentionally absent
        // (partial overlay coverage, not all-or-nothing).
        var overlay = new SemanticOverlay(
            "1.0",
            [new SemanticOverlayEntity(new PhysicalObjectIdentity("sales", "InvoiceHeader"), displayName: "Fatture")]);

        var result = CatalogMerger.Merge(catalog, overlay);

        Assert.True(result.Succeeded);
        var customerEntity = result.MergedCatalog!.Entities.Single(e => e.Physical.Identity.ObjectName == "Customers");
        Assert.Equal("Customers", customerEntity.DisplayName);
        Assert.Equal("Customer master data", customerEntity.Description);
        Assert.True(customerEntity.Exposed);
        Assert.Null(customerEntity.OData);
        Assert.Equal(["CustomerId"], customerEntity.EffectiveKeyFields);
    }

    [Fact]
    public void RelationshipsAreAUnionOfDiscoveredAndConfiguredWithCorrectProvenanceTags()
    {
        var catalog = CreateCatalog();
        var invoiceOverlay = new SemanticOverlayEntity(
            new PhysicalObjectIdentity("sales", "InvoiceHeader"),
            relationships:
            [
                new KeyValuePair<string, SemanticOverlayRelationship>(
                    "customer",
                    new SemanticOverlayRelationship(
                        new PhysicalObjectIdentity("sales", "Customers"),
                        [new SemanticOverlayJoinFieldPair("CustomerId", "CustomerId")],
                        SemanticOverlayCardinality.ManyToOne,
                        "Configured customer link")),
            ]);
        var overlay = new SemanticOverlay("1.0", [invoiceOverlay]);

        var result = CatalogMerger.Merge(catalog, overlay);

        Assert.True(result.Succeeded);
        var invoiceEntity = result.MergedCatalog!.Entities.Single(e => e.Physical.Identity.ObjectName == "InvoiceHeader");
        Assert.Equal(2, invoiceEntity.Relationships.Count);

        var discovered = invoiceEntity.Relationships.Single(r => r.Provenance == RelationshipProvenance.Discovered);
        Assert.Equal("FK_Invoices_Customer", discovered.Name);
        Assert.Null(discovered.Cardinality);
        Assert.Equal("CustomerId", discovered.FieldPairs.Single().SourceField);

        var configured = invoiceEntity.Relationships.Single(r => r.Provenance == RelationshipProvenance.Configured);
        Assert.Equal("customer", configured.Name);
        Assert.Equal(SemanticOverlayCardinality.ManyToOne, configured.Cardinality);
        Assert.Equal("Configured customer link", configured.Description);
        Assert.Equal("CustomerId", configured.FieldPairs.Single().SourceField);

        var customerEntity = result.MergedCatalog.Entities.Single(e => e.Physical.Identity.ObjectName == "Customers");
        Assert.Empty(customerEntity.Relationships);
    }

    [Fact]
    public void EffectiveKeyFieldsFollowTheOverlayOverridePhysicalPkKeylessRule()
    {
        var catalog = CreateCatalog();

        // Physical PK, no overlay key: physical PK wins.
        var noOverlay = CatalogMerger.Merge(catalog, null).MergedCatalog!;
        Assert.Equal(["InvoiceId"], noOverlay.Entities.Single(e => e.Physical.Identity.ObjectName == "InvoiceHeader").EffectiveKeyFields);
        Assert.Empty(noOverlay.Entities.Single(e => e.Physical.Identity.ObjectName == "KeylessSummary").EffectiveKeyFields);

        // Physical PK AND an overlay odata.key: the deliberate override -- overlay wins.
        var overlayWithKeyOverride = new SemanticOverlay(
            "1.0",
            [
                new SemanticOverlayEntity(
                    new PhysicalObjectIdentity("sales", "InvoiceHeader"),
                    odata: new SemanticOverlayODataSettings(key: ["CustomerId"])),
            ]);
        var overrideResult = CatalogMerger.Merge(catalog, overlayWithKeyOverride);
        Assert.True(overrideResult.Succeeded);
        Assert.Equal(
            ["CustomerId"],
            overrideResult.MergedCatalog!.Entities.Single(e => e.Physical.Identity.ObjectName == "InvoiceHeader").EffectiveKeyFields);

        // Keyless entity with an overlay key: overlay key wins.
        var overlayForKeyless = new SemanticOverlay(
            "1.0",
            [
                new SemanticOverlayEntity(
                    new PhysicalObjectIdentity("reporting", "KeylessSummary"),
                    odata: new SemanticOverlayODataSettings(key: ["Month"])),
            ]);
        var keylessResult = CatalogMerger.Merge(catalog, overlayForKeyless);
        Assert.True(keylessResult.Succeeded);
        Assert.Equal(
            ["Month"],
            keylessResult.MergedCatalog!.Entities.Single(e => e.Physical.Identity.ObjectName == "KeylessSummary").EffectiveKeyFields);
    }

    [Fact]
    public void ODataKeyFieldNotFoundIsRejected()
    {
        var catalog = CreateCatalog();
        var overlay = new SemanticOverlay(
            "1.0",
            [
                new SemanticOverlayEntity(
                    new PhysicalObjectIdentity("sales", "InvoiceHeader"),
                    odata: new SemanticOverlayODataSettings(key: ["NoSuchField"])),
            ]);

        var result = CatalogMerger.Merge(catalog, overlay);

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors);
        Assert.Equal(CatalogMergeErrorCodes.ODataKeyFieldNotFound, error.Code);
        Assert.Equal("$.entities[0].odata.key[0]", error.Path);
    }

    [Fact]
    public void CatalogVersionMismatchIsRejected()
    {
        var catalog = CreateCatalog();
        var overlay = new SemanticOverlay("2.0", []);

        var result = CatalogMerger.Merge(catalog, overlay);

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors);
        Assert.Equal(CatalogMergeErrorCodes.CatalogVersionMismatch, error.Code);
        Assert.Equal("$.catalogVersion", error.Path);
    }

    [Fact]
    public void MultipleSimultaneousProblemsAreAllCollectedInOnePass()
    {
        var catalog = CreateCatalog();
        var overlay = new SemanticOverlay(
            "1.0",
            [
                new SemanticOverlayEntity(
                    new PhysicalObjectIdentity("sales", "InvoiceHeader"),
                    odata: new SemanticOverlayODataSettings(key: ["NoSuchKeyColumn"]),
                    fields:
                    [
                        new KeyValuePair<string, SemanticOverlayField>("NoSuchColumn", new SemanticOverlayField()),
                    ],
                    relationships:
                    [
                        new KeyValuePair<string, SemanticOverlayRelationship>(
                            "ghost",
                            new SemanticOverlayRelationship(
                                new PhysicalObjectIdentity("sales", "NoSuchTable"),
                                [new SemanticOverlayJoinFieldPair("NoSuchColumn", "AlsoMissing")])),
                    ]),
            ]);

        var result = CatalogMerger.Merge(catalog, overlay);

        Assert.False(result.Succeeded);
        Assert.True(result.Errors.Count >= 4, $"Expected at least 4 errors, found {result.Errors.Count}.");
        Assert.Contains(result.Errors, e => e.Code == CatalogMergeErrorCodes.FieldNotFound);
        Assert.Contains(result.Errors, e => e.Code == CatalogMergeErrorCodes.RelationshipTargetNotFound);
        Assert.Contains(result.Errors, e => e.Code == CatalogMergeErrorCodes.JoinSourceFieldNotFound);
        Assert.Contains(result.Errors, e => e.Code == CatalogMergeErrorCodes.ODataKeyFieldNotFound);
    }

    /// <summary>
    /// Builds an overlay via <see cref="SemanticOverlayImporter"/> against one <see cref="TechnicalCatalog"/>
    /// (where it validates cleanly), then merges it against a different, trimmed-down catalog that no
    /// longer has one of the referenced entities/fields/relationship targets/join fields. Proves that
    /// <see cref="CatalogMerger.Merge"/> genuinely re-validates against the catalog it is given rather
    /// than assuming it is the overlay's original catalog -- all five reference-validation error codes
    /// must be reachable from a single such call.
    /// </summary>
    [Fact]
    public void MergeRevalidatesTheOverlayAgainstTheSuppliedCatalogRatherThanTheOriginalOne()
    {
        var originalCatalog = CreateReferenceCatalog(includeGhost: true, includeNote: true, includeProducts: true, includeOrderLinesOrderId: true, includeCustomerId: true);
        const string yaml = """
            catalogVersion: "1.0"
            entities:
              - source: "sales.Ghost"
              - source: "sales.Orders"
                fields:
                  Note:
                    displayName: "Note field"
                relationships:
                  toProducts:
                    target: "sales.Products"
                    join:
                      - sourceField: "OrderId"
                        targetField: "ProductId"
              - source: "sales.OrderLines"
                relationships:
                  toCustomer:
                    target: "sales.Customers"
                    join:
                      - sourceField: "OrderId"
                        targetField: "CustomerId"
            """;
        var importResult = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, originalCatalog);
        Assert.True(importResult.Succeeded, string.Join("; ", importResult.Errors.Select(e => e.ToString())));

        var trimmedCatalog = CreateReferenceCatalog(includeGhost: false, includeNote: false, includeProducts: false, includeOrderLinesOrderId: false, includeCustomerId: false);

        var result = CatalogMerger.Merge(trimmedCatalog, importResult.Overlay);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == CatalogMergeErrorCodes.EntitySourceNotFound);
        Assert.Contains(result.Errors, e => e.Code == CatalogMergeErrorCodes.FieldNotFound);
        Assert.Contains(result.Errors, e => e.Code == CatalogMergeErrorCodes.RelationshipTargetNotFound);
        Assert.Contains(result.Errors, e => e.Code == CatalogMergeErrorCodes.JoinSourceFieldNotFound);
        Assert.Contains(result.Errors, e => e.Code == CatalogMergeErrorCodes.JoinTargetFieldNotFound);
    }

    private static TechnicalCatalog CreateReferenceCatalog(
        bool includeGhost,
        bool includeNote,
        bool includeProducts,
        bool includeOrderLinesOrderId,
        bool includeCustomerId)
    {
        var entities = new List<TechnicalEntity>();
        if (includeGhost)
        {
            entities.Add(new TechnicalEntity(
                new PhysicalObjectIdentity("sales", "Ghost"),
                CatalogObjectKind.Table,
                [Field("X", 0, CanonicalScalarType.Int32)]));
        }

        var orderFields = new List<TechnicalField> { Field("OrderId", 0, CanonicalScalarType.Int32), Field("CustomerId", 1, CanonicalScalarType.Int32) };
        if (includeNote)
        {
            orderFields.Add(Field("Note", 2, CanonicalScalarType.String));
        }

        entities.Add(new TechnicalEntity(new PhysicalObjectIdentity("sales", "Orders"), CatalogObjectKind.Table, orderFields));

        if (includeProducts)
        {
            entities.Add(new TechnicalEntity(
                new PhysicalObjectIdentity("sales", "Products"),
                CatalogObjectKind.Table,
                [Field("ProductId", 0, CanonicalScalarType.Int32), Field("Name", 1, CanonicalScalarType.String)]));
        }

        var orderLineFields = new List<TechnicalField>();
        if (includeOrderLinesOrderId)
        {
            orderLineFields.Add(Field("OrderId", 0, CanonicalScalarType.Int32));
        }

        orderLineFields.Add(Field("ProductId", orderLineFields.Count, CanonicalScalarType.Int32));
        entities.Add(new TechnicalEntity(new PhysicalObjectIdentity("sales", "OrderLines"), CatalogObjectKind.Table, orderLineFields));

        var customerFields = new List<TechnicalField>();
        if (includeCustomerId)
        {
            customerFields.Add(Field("CustomerId", 0, CanonicalScalarType.Int32));
        }

        customerFields.Add(Field("Name", customerFields.Count, CanonicalScalarType.String));
        entities.Add(new TechnicalEntity(new PhysicalObjectIdentity("sales", "Customers"), CatalogObjectKind.Table, customerFields));

        return new TechnicalCatalog("1.0", "fixture", entities);
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

        var customers = new TechnicalEntity(
            new PhysicalObjectIdentity("sales", "Customers"),
            CatalogObjectKind.Table,
            [
                Field("CustomerId", 0, CanonicalScalarType.Int32, isIdentity: true),
                Field("Name", 1, CanonicalScalarType.String),
            ],
            [new CatalogKey("PK_Customers", ["CustomerId"], isPrimary: true)],
            description: "Customer master data");

        var keylessSummary = new TechnicalEntity(
            new PhysicalObjectIdentity("reporting", "KeylessSummary"),
            CatalogObjectKind.View,
            [
                Field("Month", 0, CanonicalScalarType.Date),
                Field("Total", 1, CanonicalScalarType.Decimal),
            ]);

        return new TechnicalCatalog("1.0", "fixture", [invoiceHeader, customers, keylessSummary]);
    }

    private static TechnicalField Field(string name, int ordinal, CanonicalScalarType type, bool isIdentity = false) =>
        new(name, ordinal, type, new ProviderTypeDetails(type.ToString(), type.ToString()), isNullable: false, isIdentity: isIdentity);
}
