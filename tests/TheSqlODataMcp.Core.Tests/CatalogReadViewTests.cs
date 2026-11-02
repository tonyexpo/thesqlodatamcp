using TheSqlODataMcp.Core.Catalog;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>
/// The exposed-only boundary (TS-01): hidden entities and relationships that target them must not leak through the
/// view protocol adapters are expected to use, while the internal merged catalog keeps the administrative metadata.
/// </summary>
public sealed class CatalogReadViewTests
{
    [Fact]
    public void HiddenEntitiesAndTheirMetadataAreAbsentFromThePublicView()
    {
        var internalCatalog = CreateCatalog();
        var view = new CatalogReadView(internalCatalog);

        Assert.Null(view.FindEntity(new PhysicalObjectIdentity("sales", "Secrets")));
        Assert.NotNull(view.FindEntity(new PhysicalObjectIdentity("sales", "Customers")));
        Assert.Equal(2, view.Catalog.Entities.Count);
        Assert.Empty(view.Search("Secrets"));
        Assert.Single(internalCatalog.Entities, entity => entity.Physical.Identity.ObjectName == "Secrets");
    }

    [Fact]
    public void RelationshipsTargetingAHiddenEntityAreRemovedFromExposedSources()
    {
        var view = new CatalogReadView(CreateCatalog());

        var orders = Assert.Single(view.Catalog.Entities, entity => entity.Physical.Identity.ObjectName == "Orders");

        Assert.DoesNotContain(orders.Relationships, relationship => relationship.Target.ObjectName == "Secrets");
        Assert.Contains(orders.Relationships, relationship => relationship.Target.ObjectName == "Customers");
    }

    private static MergedCatalog CreateCatalog()
    {
        var customers = Entity("Customers");
        var secrets = Entity("Secrets");
        var orders = Entity("Orders");
        var relationships = new[]
        {
            new MergedRelationship(
                "FK_Orders_Customer",
                customers.Identity,
                [new RelationshipFieldPair("Id", "Id")],
                RelationshipProvenance.Discovered),
            new MergedRelationship(
                "FK_Orders_Secret",
                secrets.Identity,
                [new RelationshipFieldPair("Id", "Id")],
                RelationshipProvenance.Discovered),
        };

        return new MergedCatalog(
            "1.0",
            "fixture",
            configured: true,
            [
                new MergedEntity(customers, "Customers", [new MergedField(customers.Fields[0])]),
                new MergedEntity(secrets, "Secrets", [new MergedField(secrets.Fields[0])], exposed: false),
                new MergedEntity(orders, "Orders", [new MergedField(orders.Fields[0])], relationships: relationships),
            ]);
    }

    private static TechnicalEntity Entity(string name)
    {
        var identity = new PhysicalObjectIdentity("sales", name);

        return new TechnicalEntity(
            identity,
            CatalogObjectKind.Table,
            [new TechnicalField("Id", 0, CanonicalScalarType.Int32, new ProviderTypeDetails("int", "int"), isNullable: false, isIdentity: true)],
            [new CatalogKey($"PK_{name}", ["Id"], isPrimary: true)]);
    }
}
