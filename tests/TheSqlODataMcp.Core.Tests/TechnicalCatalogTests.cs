using TheSqlODataMcp.Core.Catalog;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

public sealed class TechnicalCatalogTests
{
    [Fact]
    public void TableCatalogPreservesCompositeMetadataAndFlags()
    {
        var entity = CreateInvoiceEntity();
        var catalog = new TechnicalCatalog("1.0", "fixture", [entity]);

        Assert.Equal(CatalogObjectKind.Table, entity.Kind);
        Assert.True(entity.IsTemporal);
        Assert.Equal(["InvoiceId", "LineNumber"], entity.Keys.Single(key => key.IsPrimary).Fields);
        Assert.Equal(
            ["CustomerId", "AddressKind"],
            entity.Relationships.Single(relationship => relationship.Name == "FK_Invoices_Address").FieldPairs.Select(pair => pair.SourceField));
        Assert.True(entity.Fields.Single(field => field.Name == "InvoiceId").IsIdentity);
        Assert.True(entity.Fields.Single(field => field.Name == "LineTotal").IsComputed);
        Assert.True(entity.Fields.Single(field => field.Name == "LineTotal").IsPersistedComputed);
        Assert.True(entity.Fields.Single(field => field.Name == "ValidFrom").IsTemporalPeriodStart);
        Assert.True(entity.Fields.Single(field => field.Name == "ValidTo").IsTemporalPeriodEnd);
        Assert.True(entity.Fields.Single(field => field.Name == "Version").IsRowVersion);
        Assert.True(entity.Indexes.Single(index => index.Name == "UX_Invoices_Number").IsFiltered);
        Assert.Equal("sales.Invoices", catalog.Entities.Single().Identity.ToString());
    }

    [Fact]
    public void KeylessViewIsValidWithoutSynthesizingAKey()
    {
        var view = new TechnicalEntity(
            new PhysicalObjectIdentity("reporting", "InvoiceMonthlySummary"),
            CatalogObjectKind.View,
            [
                Field("Month", 0, CanonicalScalarType.Date),
                Field("InvoiceCount", 1, CanonicalScalarType.Int64),
            ]);

        var catalog = new TechnicalCatalog("1.0", "fixture", [view]);

        Assert.Empty(catalog.Entities.Single().Keys);
        Assert.Equal(CatalogObjectKind.View, catalog.Entities.Single().Kind);
    }

    [Fact]
    public void CanonicalJsonUsesExactScalarWireNames()
    {
        var types = Enum.GetValues<CanonicalScalarType>();
        var fields = types.Select((type, ordinal) => Field(type.ToString(), ordinal, type)).ToArray();
        var catalog = new TechnicalCatalog(
            "1.0",
            "fixture",
            [new TechnicalEntity(new PhysicalObjectIdentity("types", "AllScalars"), CatalogObjectKind.Table, fields)]);

        var json = TechnicalCatalogCanonicalJson.Serialize(catalog);

        foreach (var wireName in new[]
                 {
                     "boolean", "int16", "int32", "int64", "decimal", "double", "string", "guid", "date", "time",
                     "datetime", "datetimeOffset", "binary", "json", "unknown",
                 })
        {
            Assert.Contains(string.Concat("\"canonicalType\":\"", wireName, "\""), json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CanonicalJsonAndHashIgnoreUnorderedInputEnumeration()
    {
        var invoice = CreateInvoiceEntity();
        var customer = new TechnicalEntity(
            new PhysicalObjectIdentity("crm", "Customers"),
            CatalogObjectKind.Table,
            [Field("CustomerId", 0, CanonicalScalarType.Int32)],
            [new CatalogKey("PK_Customers", ["CustomerId"], isPrimary: true)]);
        var first = new TechnicalCatalog("1.0", "fixture", [invoice, customer]);
        var second = new TechnicalCatalog(
            "1.0",
            "fixture",
            [customer, CreateInvoiceEntity(reverseFields: true, reverseNamedMetadata: true)]);

        Assert.Equal(TechnicalCatalogCanonicalJson.Serialize(first), TechnicalCatalogCanonicalJson.Serialize(second));
        Assert.Equal(
            TechnicalCatalogCanonicalJson.ComputeStructuralHash(first),
            TechnicalCatalogCanonicalJson.ComputeStructuralHash(second));
    }

    [Fact]
    public void OrderedKeyColumnsAndRelationshipPairsAffectTheHash()
    {
        var standard = new TechnicalCatalog("1.0", "fixture", [CreateInvoiceEntity()]);
        var reordered = new TechnicalCatalog("1.0", "fixture", [CreateInvoiceEntity(reverseKeyAndRelationshipPairs: true)]);

        Assert.NotEqual(
            TechnicalCatalogCanonicalJson.ComputeStructuralHash(standard),
            TechnicalCatalogCanonicalJson.ComputeStructuralHash(reordered));
    }

    [Fact]
    public void FilteredIndexSurvivesCanonicalSerializationAndAffectsTheHash()
    {
        var filtered = new TechnicalCatalog("1.0", "fixture", [CreateInvoiceEntity()]);
        var unfiltered = new TechnicalCatalog("1.0", "fixture", [CreateInvoiceEntity(isFilteredIndex: false)]);

        Assert.Contains("\"isFiltered\":true", TechnicalCatalogCanonicalJson.Serialize(filtered), StringComparison.Ordinal);
        Assert.NotEqual(
            TechnicalCatalogCanonicalJson.ComputeStructuralHash(filtered),
            TechnicalCatalogCanonicalJson.ComputeStructuralHash(unfiltered));
    }

    [Fact]
    public void ConstructorsDefensivelyCopyCollectionInputs()
    {
        var fields = new List<TechnicalField>
        {
            Field("InvoiceId", 0, CanonicalScalarType.Int32),
        };
        var keyFields = new List<string> { "InvoiceId" };
        var keys = new List<CatalogKey> { new("PK_Invoices", keyFields, isPrimary: true) };
        var entity = new TechnicalEntity(
            new PhysicalObjectIdentity("sales", "Invoices"),
            CatalogObjectKind.Table,
            fields,
            keys);
        var entities = new List<TechnicalEntity> { entity };
        var catalog = new TechnicalCatalog("1.0", "fixture", entities);

        fields.Add(Field("Ignored", 1, CanonicalScalarType.String));
        keyFields.Add("Ignored");
        keys.Clear();
        entities.Clear();

        Assert.Single(catalog.Entities);
        Assert.Single(catalog.Entities.Single().Fields);
        Assert.Equal(["InvoiceId"], catalog.Entities.Single().Keys.Single().Fields);
        Assert.Throws<NotSupportedException>(() => ((IList<TechnicalEntity>)catalog.Entities).Add(entity));
    }

    [Fact]
    public void ConstructionRejectsStructuralInvalidity()
    {
        var field = Field("Id", 0, CanonicalScalarType.Int32);
        var identity = new PhysicalObjectIdentity("sales", "Invoices");

        Assert.Throws<ArgumentException>(() => new PhysicalObjectIdentity(" ", "Invoices"));
        Assert.Throws<ArgumentException>(() => new TechnicalCatalog(" ", "fixture", []));
        Assert.Throws<ArgumentException>(() => new TechnicalCatalog("1.0", " ", []));
        Assert.Throws<ArgumentOutOfRangeException>(() => Field("Id", -1, CanonicalScalarType.Int32));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProviderTypeDetails("int", "int", length: -1));
        Assert.Throws<ArgumentException>(() => Field("Persisted", 1, CanonicalScalarType.Decimal, isPersistedComputed: true));
        Assert.Throws<ArgumentException>(() => Field("Period", 1, CanonicalScalarType.DateTime, isTemporalPeriodStart: true, isTemporalPeriodEnd: true));
        Assert.Throws<ArgumentException>(() => Field("IdentityComputed", 1, CanonicalScalarType.Int32, isIdentity: true, isComputed: true));
        Assert.Throws<ArgumentException>(() => Field("Version", 1, CanonicalScalarType.Int64, isRowVersion: true));
        Assert.Throws<ArgumentException>(() => Field("Version", 1, CanonicalScalarType.Binary, isIdentity: true, isRowVersion: true));
        Assert.Throws<ArgumentException>(() => Field("Version", 1, CanonicalScalarType.Binary, isComputed: true, isRowVersion: true));
        Assert.Throws<ArgumentException>(() => new CatalogKey("PK", ["Id", "Id"]));
        Assert.Throws<ArgumentException>(() => new CatalogIndex("IX", ["Id", "Id"], isUnique: false));
        Assert.Throws<ArgumentException>(() => new RelationshipFieldPair(" ", "Id"));
        Assert.Throws<ArgumentException>(() => new CatalogRelationship("FK", identity, [new RelationshipFieldPair("Id", "Id"), new RelationshipFieldPair("Id", "Other")]));
        Assert.Throws<ArgumentException>(() => new CatalogRelationship("FK", identity, [new RelationshipFieldPair("Id", "Id"), new RelationshipFieldPair("Other", "Id")]));
        Assert.Throws<ArgumentException>(() => new TechnicalEntity(identity, CatalogObjectKind.Table, [field, Field("Id", 1, CanonicalScalarType.Int32)]));
        Assert.Throws<ArgumentException>(() => new TechnicalEntity(
            identity,
            CatalogObjectKind.Table,
            [field, Field("ValidFrom", 1, CanonicalScalarType.DateTime, isTemporalPeriodStart: true)]));
        Assert.Throws<ArgumentException>(() => new TechnicalEntity(identity, CatalogObjectKind.Table, [field], isTemporal: true));
        Assert.Throws<ArgumentException>(() => new TechnicalEntity(
            identity,
            CatalogObjectKind.Table,
            [field, Field("ValidFrom", 1, CanonicalScalarType.DateTime, isTemporalPeriodStart: true)],
            isTemporal: true));
        Assert.Throws<ArgumentException>(() => new TechnicalEntity(
            identity,
            CatalogObjectKind.Table,
            [
                field,
                Field("ValidFrom", 1, CanonicalScalarType.DateTime, isTemporalPeriodStart: true),
                Field("ValidTo", 2, CanonicalScalarType.DateTime, isTemporalPeriodEnd: true),
                Field("OtherValidTo", 3, CanonicalScalarType.DateTime, isTemporalPeriodEnd: true),
            ],
            isTemporal: true));
        Assert.Throws<ArgumentException>(() => new TechnicalEntity(identity, CatalogObjectKind.Table, [field], [new CatalogKey("PK", ["Missing"])]));
        Assert.Throws<ArgumentException>(() => new TechnicalEntity(
            identity,
            CatalogObjectKind.Table,
            [field],
            relationships: [new CatalogRelationship("FK", new PhysicalObjectIdentity("crm", "Customers"), [new RelationshipFieldPair("Missing", "Id")])]));
        Assert.Throws<ArgumentException>(() => new TechnicalEntity(
            identity,
            CatalogObjectKind.Table,
            [field],
            keys: [new CatalogKey("PK", ["Id"]), new CatalogKey("PK", ["Id"])]));
        Assert.Throws<ArgumentException>(() => new TechnicalEntity(
            identity,
            CatalogObjectKind.Table,
            [field],
            indexes: [new CatalogIndex("IX", ["Id"], isUnique: false), new CatalogIndex("IX", ["Id"], isUnique: true)]));
        Assert.Throws<ArgumentException>(() => new TechnicalEntity(
            identity,
            CatalogObjectKind.Table,
            [field],
            relationships:
            [
                new CatalogRelationship("FK", new PhysicalObjectIdentity("crm", "Customers"), [new RelationshipFieldPair("Id", "Id")]),
                new CatalogRelationship("FK", new PhysicalObjectIdentity("crm", "Orders"), [new RelationshipFieldPair("Id", "Id")]),
            ]));
        Assert.Throws<ArgumentException>(() => new TechnicalCatalog(
            "1.0",
            "fixture",
            [new TechnicalEntity(identity, CatalogObjectKind.Table, [field]), new TechnicalEntity(identity, CatalogObjectKind.Table, [field])]));
    }

    private static TechnicalEntity CreateInvoiceEntity(
        bool reverseFields = false,
        bool reverseNamedMetadata = false,
        bool reverseKeyAndRelationshipPairs = false,
        bool isFilteredIndex = true)
    {
        var fields = new[]
        {
            Field("InvoiceId", 0, CanonicalScalarType.Int32, isIdentity: true),
            Field("LineNumber", 1, CanonicalScalarType.Int16),
            Field("CustomerId", 2, CanonicalScalarType.Int32),
            Field("AddressKind", 3, CanonicalScalarType.String),
            Field("LineTotal", 4, CanonicalScalarType.Decimal, isComputed: true, isPersistedComputed: true),
            Field("ValidFrom", 5, CanonicalScalarType.DateTime, isTemporalPeriodStart: true),
            Field("ValidTo", 6, CanonicalScalarType.DateTime, isTemporalPeriodEnd: true),
            Field("Version", 7, CanonicalScalarType.Binary, isRowVersion: true),
        };
        if (reverseFields)
        {
            Array.Reverse(fields);
        }

        var keyFields = reverseKeyAndRelationshipPairs ? new[] { "LineNumber", "InvoiceId" } : new[] { "InvoiceId", "LineNumber" };
        var pairFields = reverseKeyAndRelationshipPairs
            ? new[] { new RelationshipFieldPair("AddressKind", "AddressKind"), new RelationshipFieldPair("CustomerId", "CustomerId") }
            : new[] { new RelationshipFieldPair("CustomerId", "CustomerId"), new RelationshipFieldPair("AddressKind", "AddressKind") };
        var keys = new[]
        {
            new CatalogKey("PK_Invoices", keyFields, isPrimary: true),
            new CatalogKey("AK_Invoices_Customer", ["CustomerId", "InvoiceId"]),
        };
        var indexes = new[]
        {
            new CatalogIndex("IX_Invoices_Customer", ["CustomerId", "InvoiceId"], isUnique: false),
            new CatalogIndex("UX_Invoices_Number", ["InvoiceId", "LineNumber"], isUnique: true, isFiltered: isFilteredIndex),
        };
        var relationships = new[]
        {
            new CatalogRelationship(
                "FK_Invoices_Address",
                new PhysicalObjectIdentity("crm", "CustomerAddresses"),
                pairFields),
            new CatalogRelationship(
                "FK_Invoices_Customer",
                new PhysicalObjectIdentity("crm", "Customers"),
                [new RelationshipFieldPair("CustomerId", "CustomerId")]),
        };

        if (reverseNamedMetadata)
        {
            Array.Reverse(keys);
            Array.Reverse(indexes);
            Array.Reverse(relationships);
        }

        return new TechnicalEntity(
            new PhysicalObjectIdentity("sales", "Invoices"),
            CatalogObjectKind.Table,
            fields,
            keys,
            indexes,
            relationships,
            description: "Invoice lines",
            isTemporal: true);
    }

    private static TechnicalField Field(
        string name,
        int ordinal,
        CanonicalScalarType type,
        bool isIdentity = false,
        bool isComputed = false,
        bool isPersistedComputed = false,
        bool isTemporalPeriodStart = false,
        bool isTemporalPeriodEnd = false,
        bool isRowVersion = false) =>
        new(
            name,
            ordinal,
            type,
            new ProviderTypeDetails(type.ToString(), type.ToString()),
            isNullable: false,
            isIdentity: isIdentity,
            isComputed: isComputed,
            isPersistedComputed: isPersistedComputed,
            isTemporalPeriodStart: isTemporalPeriodStart,
            isTemporalPeriodEnd: isTemporalPeriodEnd,
            isRowVersion: isRowVersion);
}
