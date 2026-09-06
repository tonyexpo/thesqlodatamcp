using TheSqlODataMcp.Core.Catalog;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>Behavior tests for <see cref="CatalogSearchIndex"/>.</summary>
public sealed class CatalogSearchIndexTests
{
    [Fact]
    public void FindEntityReturnsTheMatchingEntityOrNull()
    {
        var index = new CatalogSearchIndex(CreateCatalog());

        var found = index.FindEntity(new PhysicalObjectIdentity("sales", "Customers"));
        var missing = index.FindEntity(new PhysicalObjectIdentity("sales", "Products"));

        Assert.NotNull(found);
        Assert.Equal("Clienti", found!.DisplayName);
        Assert.Null(missing);
    }

    [Fact]
    public void SearchReturnsEmptyForAWhitespaceOrMissingQuery()
    {
        var index = new CatalogSearchIndex(CreateCatalog());

        Assert.Empty(index.Search(" "));
        Assert.Empty(index.Search(string.Empty));
    }

    [Fact]
    public void SearchRanksAnExactNameMatchAboveAPartialDescriptionMatch()
    {
        var index = new CatalogSearchIndex(CreateCatalog());

        var matches = index.Search("Customers");

        Assert.Equal(CatalogSearchMatchKind.EntityName, matches[0].Kind);
        Assert.Equal("Customers", matches[0].MatchedText);
        Assert.True(matches[0].Score > matches[^1].Score);
    }

    [Fact]
    public void SearchFindsAFieldByAlias()
    {
        var index = new CatalogSearchIndex(CreateCatalog());

        var matches = index.Search("client-id");

        var match = Assert.Single(matches);
        Assert.Equal(CatalogSearchMatchKind.FieldAlias, match.Kind);
        Assert.Equal("Customers", match.Entity!.Physical.Identity.ObjectName);
        Assert.Equal("CustomerId", match.Field!.Physical.Name);
        Assert.Null(match.Relationship);
    }

    [Fact]
    public void SearchFindsARelationshipByName()
    {
        var index = new CatalogSearchIndex(CreateCatalog());

        var matches = index.Search("FK_Orders_Customer");

        var match = Assert.Single(matches);
        Assert.Equal(CatalogSearchMatchKind.Relationship, match.Kind);
        Assert.Equal("Orders", match.Entity!.Physical.Identity.ObjectName);
        Assert.Equal("FK_Orders_Customer", match.Relationship!.Name);
        Assert.Null(match.Field);
    }

    [Fact]
    public void SearchFindsACatalogLevelWarningWithNoEntity()
    {
        var index = new CatalogSearchIndex(CreateCatalog());

        var matches = index.Search("unmapped");

        var match = Assert.Single(matches);
        Assert.Equal(CatalogSearchMatchKind.Warning, match.Kind);
        Assert.Null(match.Entity);
    }

    [Fact]
    public void SearchDoesNotDuplicateAMatchWhenDisplayNameEqualsThePhysicalName()
    {
        var customers = new TechnicalEntity(
            new PhysicalObjectIdentity("sales", "Widgets"),
            CatalogObjectKind.Table,
            [Field("Widgets", 0, CanonicalScalarType.Int32, isIdentity: true)]);
        var entity = new MergedEntity(customers, "Widgets", [new MergedField(customers.Fields[0])]);
        var catalog = new MergedCatalog("1.0", "fixture", configured: false, [entity]);
        var index = new CatalogSearchIndex(catalog);

        var matches = index.Search("Widgets");

        Assert.Single(matches, match => match.Kind == CatalogSearchMatchKind.EntityName);
    }

    [Fact]
    public void SearchReturnsBothMatchesWhenTwoDifferentFieldsShareTheSameDisplayName()
    {
        var physical = new TechnicalEntity(
            new PhysicalObjectIdentity("sales", "Products"),
            CatalogObjectKind.Table,
            [
                Field("Sku", 0, CanonicalScalarType.String, isIdentity: true),
                Field("VendorSku", 1, CanonicalScalarType.String),
            ]);
        var entity = new MergedEntity(
            physical,
            "Products",
            [
                new MergedField(physical.Fields[0], displayName: "Shared Label"),
                new MergedField(physical.Fields[1], displayName: "Shared Label"),
            ]);
        var index = new CatalogSearchIndex(new MergedCatalog("1.0", "fixture", configured: false, [entity]));

        var matches = index.Search("Shared Label");

        Assert.Equal(2, matches.Count);
        var matchedFieldNames = matches.Select(match => match.Field!.Physical.Name).ToHashSet();
        Assert.Equal(new HashSet<string> { "Sku", "VendorSku" }, matchedFieldNames);
    }

    [Fact]
    public void SearchRejectsANonPositiveMaxResults()
    {
        var index = new CatalogSearchIndex(CreateCatalog());

        Assert.Throws<ArgumentOutOfRangeException>(() => index.Search("Customers", maxResults: 0));
    }

    [Fact]
    public void ConstructionRejectsNullInputs()
    {
        Assert.Throws<ArgumentNullException>(() => new CatalogSearchIndex(null!));
        Assert.Throws<ArgumentNullException>(() => new CatalogSearchIndex(CreateCatalog()).FindEntity(null!));
    }

    [Fact]
    public void MatchConstructionRejectsInvalidInput()
    {
        var entity = CreateCatalog().Entities[0];
        var field = new MergedField(entity.Physical.Fields[0]);
        var relationship = new MergedRelationship(
            "r", new PhysicalObjectIdentity("sales", "Customers"), [new RelationshipFieldPair("CustomerId", "CustomerId")], RelationshipProvenance.Discovered);

        Assert.Throws<ArgumentException>(() => new CatalogSearchMatch(entity, CatalogSearchMatchKind.EntityName, " ", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CatalogSearchMatch(entity, CatalogSearchMatchKind.EntityName, "x", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CatalogSearchMatch(entity, (CatalogSearchMatchKind)int.MaxValue, "x", 1));
        Assert.Throws<ArgumentException>(() => new CatalogSearchMatch(null, CatalogSearchMatchKind.FieldName, "x", 1, field: field));
        Assert.Throws<ArgumentException>(() => new CatalogSearchMatch(null, CatalogSearchMatchKind.Relationship, "x", 1, relationship: relationship));
    }

    private static MergedCatalog CreateCatalog()
    {
        var customers = new TechnicalEntity(
            new PhysicalObjectIdentity("sales", "Customers"),
            CatalogObjectKind.Table,
            [
                new TechnicalField("CustomerId", 0, CanonicalScalarType.Int32, new ProviderTypeDetails("int", "int"), isNullable: false, isIdentity: true),
                new TechnicalField("Name", 1, CanonicalScalarType.String, new ProviderTypeDetails("nvarchar", "nvarchar(100)", length: 100), isNullable: false),
            ],
            [new CatalogKey("PK_Customers", ["CustomerId"], isPrimary: true)]);
        var orders = new TechnicalEntity(
            new PhysicalObjectIdentity("sales", "Orders"),
            CatalogObjectKind.Table,
            [
                new TechnicalField("OrderId", 0, CanonicalScalarType.Int32, new ProviderTypeDetails("int", "int"), isNullable: false, isIdentity: true),
                new TechnicalField("CustomerId", 1, CanonicalScalarType.Int32, new ProviderTypeDetails("int", "int"), isNullable: false),
            ],
            [new CatalogKey("PK_Orders", ["OrderId"], isPrimary: true)],
            relationships:
            [
                new CatalogRelationship(
                    "FK_Orders_Customer",
                    new PhysicalObjectIdentity("sales", "Customers"),
                    [new RelationshipFieldPair("CustomerId", "CustomerId")]),
            ]);

        var customersEntity = new MergedEntity(
            customers,
            "Clienti",
            [
                new MergedField(customers.Fields[0], aliases: ["client-id"]),
                new MergedField(customers.Fields[1]),
            ],
            description: "Stores customers and their orders");
        var ordersEntity = new MergedEntity(
            orders,
            "Orders",
            [new MergedField(orders.Fields[0]), new MergedField(orders.Fields[1])],
            relationships:
            [
                new MergedRelationship(
                    "FK_Orders_Customer",
                    new PhysicalObjectIdentity("sales", "Customers"),
                    [new RelationshipFieldPair("CustomerId", "CustomerId")],
                    RelationshipProvenance.Discovered),
            ]);

        return new MergedCatalog(
            "1.0",
            "fixture",
            configured: true,
            [customersEntity, ordersEntity],
            warnings: [new SemanticOverlayWarning("Unmapped table", "The table sales.Legacy has no overlay entry.")]);
    }

    private static TechnicalField Field(string name, int ordinal, CanonicalScalarType type, bool isIdentity = false) =>
        new(name, ordinal, type, new ProviderTypeDetails(type.ToString(), type.ToString()), isNullable: false, isIdentity: isIdentity);
}
