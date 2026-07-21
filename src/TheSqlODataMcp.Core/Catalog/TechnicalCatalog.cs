using System.Collections.ObjectModel;

namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// A versioned, provider-neutral snapshot of physical reporting objects.
/// </summary>
public sealed class TechnicalCatalog
{
    private readonly IReadOnlyList<TechnicalEntity> entities;

    public TechnicalCatalog(string catalogVersion, string provider, IEnumerable<TechnicalEntity> entities)
    {
        CatalogVersion = RequireIdentifier(catalogVersion, nameof(catalogVersion));
        Provider = RequireIdentifier(provider, nameof(provider));
        this.entities = CopyDistinct(
            entities,
            entity => entity.Identity,
            nameof(entities),
            PhysicalObjectIdentityComparer.Instance);
    }

    public string CatalogVersion { get; }

    public string Provider { get; }

    public IReadOnlyList<TechnicalEntity> Entities => entities;

    internal static string RequireIdentifier(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }

    internal static IReadOnlyList<T> CopyDistinct<T, TKey>(
        IEnumerable<T> values,
        Func<T, TKey> keySelector,
        string parameterName,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        ArgumentNullException.ThrowIfNull(keySelector);

        var copy = values.ToArray();
        if (copy.Any(static value => value is null))
        {
            throw new ArgumentException("Collections cannot contain null values.", parameterName);
        }

        var seen = new HashSet<TKey>(comparer);
        foreach (var value in copy)
        {
            if (!seen.Add(keySelector(value)))
            {
                throw new ArgumentException("Collections cannot contain duplicate values.", parameterName);
            }
        }

        return new ReadOnlyCollection<T>(copy);
    }

    private sealed class PhysicalObjectIdentityComparer : IEqualityComparer<PhysicalObjectIdentity>
    {
        public static PhysicalObjectIdentityComparer Instance { get; } = new();

        public bool Equals(PhysicalObjectIdentity? x, PhysicalObjectIdentity? y) =>
            x is null
                ? y is null
                : y is not null
                    && string.Equals(x.Schema, y.Schema, StringComparison.Ordinal)
                    && string.Equals(x.ObjectName, y.ObjectName, StringComparison.Ordinal);

        public int GetHashCode(PhysicalObjectIdentity obj) =>
            HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(obj.Schema),
                StringComparer.Ordinal.GetHashCode(obj.ObjectName));
    }
}

/// <summary>
/// Stable physical identity for a table or view.
/// </summary>
public sealed class PhysicalObjectIdentity : IEquatable<PhysicalObjectIdentity>
{
    public PhysicalObjectIdentity(string schema, string objectName)
    {
        Schema = TechnicalCatalog.RequireIdentifier(schema, nameof(schema));
        ObjectName = TechnicalCatalog.RequireIdentifier(objectName, nameof(objectName));
    }

    public string Schema { get; }

    public string ObjectName { get; }

    public bool Equals(PhysicalObjectIdentity? other) =>
        other is not null
        && string.Equals(Schema, other.Schema, StringComparison.Ordinal)
        && string.Equals(ObjectName, other.ObjectName, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as PhysicalObjectIdentity);

    public override int GetHashCode() => HashCode.Combine(
        StringComparer.Ordinal.GetHashCode(Schema),
        StringComparer.Ordinal.GetHashCode(ObjectName));

    public override string ToString() => string.Concat(Schema, ".", ObjectName);
}

public enum CatalogObjectKind
{
    Table,
    View,
}

/// <summary>
/// A provider-neutral scalar classification used by catalog consumers.
/// </summary>
#pragma warning disable CA1720 // Canonical names intentionally mirror the public catalog type vocabulary.
public enum CanonicalScalarType
{
    Boolean,
    Int16,
    Int32,
    Int64,
    Decimal,
    Double,
    String,
    Guid,
    Date,
    Time,
    DateTime,
    DateTimeOffset,
    Binary,
    Json,
    Unknown,
}
#pragma warning restore CA1720

/// <summary>
/// Provider type detail retained alongside the canonical scalar classification.
/// </summary>
public sealed class ProviderTypeDetails
{
    public ProviderTypeDetails(
        string name,
        string storeRepresentation,
        int? length = null,
        int? precision = null,
        int? scale = null)
    {
        Name = TechnicalCatalog.RequireIdentifier(name, nameof(name));
        StoreRepresentation = TechnicalCatalog.RequireIdentifier(storeRepresentation, nameof(storeRepresentation));
        Length = RequireNonNegative(length, nameof(length));
        Precision = RequireNonNegative(precision, nameof(precision));
        Scale = RequireNonNegative(scale, nameof(scale));
    }

    public string Name { get; }

    public string StoreRepresentation { get; }

    public int? Length { get; }

    public int? Precision { get; }

    public int? Scale { get; }

    private static int? RequireNonNegative(int? value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The value cannot be negative.");
        }

        return value;
    }
}

public sealed class TechnicalField
{
    public TechnicalField(
        string name,
        int ordinal,
        CanonicalScalarType canonicalType,
        ProviderTypeDetails providerType,
        bool isNullable,
        string? description = null,
        bool isIdentity = false,
        bool isComputed = false,
        bool isPersistedComputed = false,
        bool isTemporalPeriodStart = false,
        bool isTemporalPeriodEnd = false,
        bool isRowVersion = false)
    {
        Name = TechnicalCatalog.RequireIdentifier(name, nameof(name));
        if (!Enum.IsDefined(canonicalType))
        {
            throw new ArgumentOutOfRangeException(nameof(canonicalType), "The canonical scalar type is not supported.");
        }

        if (ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal), "The ordinal cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(providerType);
        if (isPersistedComputed && !isComputed)
        {
            throw new ArgumentException("A persisted computed field must be computed.", nameof(isPersistedComputed));
        }

        if (isTemporalPeriodStart && isTemporalPeriodEnd)
        {
            throw new ArgumentException("A field cannot be both temporal period start and end.", nameof(isTemporalPeriodEnd));
        }

        if (isIdentity && isComputed)
        {
            throw new ArgumentException("An identity field cannot be computed.", nameof(isComputed));
        }

        if (isRowVersion && canonicalType != CanonicalScalarType.Binary)
        {
            throw new ArgumentException("A rowversion field must have the binary canonical type.", nameof(canonicalType));
        }

        if (isRowVersion && (isIdentity || isComputed))
        {
            throw new ArgumentException("A rowversion field cannot be an identity or computed field.", nameof(isRowVersion));
        }

        Ordinal = ordinal;
        CanonicalType = canonicalType;
        ProviderType = providerType;
        IsNullable = isNullable;
        Description = description;
        IsIdentity = isIdentity;
        IsComputed = isComputed;
        IsPersistedComputed = isPersistedComputed;
        IsTemporalPeriodStart = isTemporalPeriodStart;
        IsTemporalPeriodEnd = isTemporalPeriodEnd;
        IsRowVersion = isRowVersion;
    }

    public string Name { get; }

    public int Ordinal { get; }

    public CanonicalScalarType CanonicalType { get; }

    public ProviderTypeDetails ProviderType { get; }

    public bool IsNullable { get; }

    public string? Description { get; }

    public bool IsIdentity { get; }

    public bool IsComputed { get; }

    public bool IsPersistedComputed { get; }

    public bool IsTemporalPeriodStart { get; }

    public bool IsTemporalPeriodEnd { get; }

    public bool IsRowVersion { get; }
}

public sealed class CatalogKey
{
    private readonly IReadOnlyList<string> fields;

    public CatalogKey(string name, IEnumerable<string> fields, bool isPrimary = false)
    {
        Name = TechnicalCatalog.RequireIdentifier(name, nameof(name));
        this.fields = CopyFieldNames(fields, nameof(fields));
        IsPrimary = isPrimary;
    }

    public string Name { get; }

    public IReadOnlyList<string> Fields => fields;

    public bool IsPrimary { get; }

    internal static IReadOnlyList<string> CopyFieldNames(IEnumerable<string> fields, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(fields, parameterName);
        var copy = fields.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("At least one field is required.", parameterName);
        }

        return TechnicalCatalog.CopyDistinct(
            copy.Select(field => TechnicalCatalog.RequireIdentifier(field, parameterName)),
            static field => field,
            parameterName,
            StringComparer.Ordinal);
    }
}

public sealed class CatalogIndex
{
    private readonly IReadOnlyList<string> fields;

    public CatalogIndex(string name, IEnumerable<string> fields, bool isUnique, string? description = null, bool isFiltered = false)
    {
        Name = TechnicalCatalog.RequireIdentifier(name, nameof(name));
        this.fields = CatalogKey.CopyFieldNames(fields, nameof(fields));
        IsUnique = isUnique;
        Description = description;
        IsFiltered = isFiltered;
    }

    public string Name { get; }

    public IReadOnlyList<string> Fields => fields;

    public bool IsUnique { get; }

    public bool IsFiltered { get; }

    public string? Description { get; }
}

public sealed class RelationshipFieldPair
{
    public RelationshipFieldPair(string sourceField, string targetField)
    {
        SourceField = TechnicalCatalog.RequireIdentifier(sourceField, nameof(sourceField));
        TargetField = TechnicalCatalog.RequireIdentifier(targetField, nameof(targetField));
    }

    public string SourceField { get; }

    public string TargetField { get; }
}

public sealed class CatalogRelationship
{
    private readonly IReadOnlyList<RelationshipFieldPair> fieldPairs;

    public CatalogRelationship(
        string name,
        PhysicalObjectIdentity target,
        IEnumerable<RelationshipFieldPair> fieldPairs,
        string? description = null)
    {
        Name = TechnicalCatalog.RequireIdentifier(name, nameof(name));
        ArgumentNullException.ThrowIfNull(target);
        Target = target;
        this.fieldPairs = CopyPairs(fieldPairs);
        Description = description;
    }

    public string Name { get; }

    public PhysicalObjectIdentity Target { get; }

    public IReadOnlyList<RelationshipFieldPair> FieldPairs => fieldPairs;

    public string? Description { get; }

    private static ReadOnlyCollection<RelationshipFieldPair> CopyPairs(IEnumerable<RelationshipFieldPair> fieldPairs)
    {
        ArgumentNullException.ThrowIfNull(fieldPairs);
        var copy = fieldPairs.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("At least one relationship field pair is required.", nameof(fieldPairs));
        }

        var seenPairs = new HashSet<(string Source, string Target)>();
        var seenSources = new HashSet<string>(StringComparer.Ordinal);
        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in copy)
        {
            ArgumentNullException.ThrowIfNull(pair, nameof(fieldPairs));
            if (!seenPairs.Add((pair.SourceField, pair.TargetField))
                || !seenSources.Add(pair.SourceField)
                || !seenTargets.Add(pair.TargetField))
            {
                throw new ArgumentException("Relationship field pairs cannot be duplicated.", nameof(fieldPairs));
            }
        }

        return new ReadOnlyCollection<RelationshipFieldPair>(copy);
    }
}

public sealed class TechnicalEntity
{
    private readonly IReadOnlyList<TechnicalField> fields;
    private readonly IReadOnlyList<CatalogKey> keys;
    private readonly IReadOnlyList<CatalogIndex> indexes;
    private readonly IReadOnlyList<CatalogRelationship> relationships;

    public TechnicalEntity(
        PhysicalObjectIdentity identity,
        CatalogObjectKind kind,
        IEnumerable<TechnicalField> fields,
        IEnumerable<CatalogKey>? keys = null,
        IEnumerable<CatalogIndex>? indexes = null,
        IEnumerable<CatalogRelationship>? relationships = null,
        string? description = null,
        bool isTemporal = false)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "The object kind is not supported.");
        }

        Identity = identity;
        Kind = kind;
        this.fields = TechnicalCatalog.CopyDistinct(
            fields,
            field => field.Name,
            nameof(fields),
            StringComparer.Ordinal);
        if (this.fields.Count == 0)
        {
            throw new ArgumentException("An entity must have at least one field.", nameof(fields));
        }

        if (this.fields.Select(field => field.Ordinal).Distinct().Count() != this.fields.Count)
        {
            throw new ArgumentException("Field ordinals must be unique within an entity.", nameof(fields));
        }

        var temporalPeriodStartCount = this.fields.Count(field => field.IsTemporalPeriodStart);
        var temporalPeriodEndCount = this.fields.Count(field => field.IsTemporalPeriodEnd);
        if (!isTemporal && (temporalPeriodStartCount != 0 || temporalPeriodEndCount != 0))
        {
            throw new ArgumentException("Temporal period fields require a temporal entity.", nameof(isTemporal));
        }

        if (isTemporal && (temporalPeriodStartCount != 1 || temporalPeriodEndCount != 1))
        {
            throw new ArgumentException("A temporal entity must have exactly one period start and one period end field.", nameof(fields));
        }

        this.keys = TechnicalCatalog.CopyDistinct(
            keys ?? Array.Empty<CatalogKey>(),
            key => key.Name,
            nameof(keys),
            StringComparer.Ordinal);
        this.indexes = TechnicalCatalog.CopyDistinct(
            indexes ?? Array.Empty<CatalogIndex>(),
            index => index.Name,
            nameof(indexes),
            StringComparer.Ordinal);
        this.relationships = TechnicalCatalog.CopyDistinct(
            relationships ?? Array.Empty<CatalogRelationship>(),
            relationship => relationship.Name,
            nameof(relationships),
            StringComparer.Ordinal);

        ValidateFieldReferences(this.keys.SelectMany(key => key.Fields), nameof(keys));
        ValidateFieldReferences(this.indexes.SelectMany(index => index.Fields), nameof(indexes));
        ValidateFieldReferences(this.relationships.SelectMany(relationship => relationship.FieldPairs.Select(pair => pair.SourceField)), nameof(relationships));

        if (this.keys.Count(key => key.IsPrimary) > 1)
        {
            throw new ArgumentException("An entity cannot have more than one primary key.", nameof(keys));
        }

        Description = description;
        IsTemporal = isTemporal;
    }

    public PhysicalObjectIdentity Identity { get; }

    public CatalogObjectKind Kind { get; }

    public IReadOnlyList<TechnicalField> Fields => fields;

    public IReadOnlyList<CatalogKey> Keys => keys;

    public IReadOnlyList<CatalogIndex> Indexes => indexes;

    public IReadOnlyList<CatalogRelationship> Relationships => relationships;

    public string? Description { get; }

    public bool IsTemporal { get; }

    private void ValidateFieldReferences(IEnumerable<string> names, string parameterName)
    {
        var fieldNames = new HashSet<string>(fields.Select(field => field.Name), StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (!fieldNames.Contains(name))
            {
                throw new ArgumentException($"The field '{name}' is not present on entity '{Identity}'.", parameterName);
            }
        }
    }
}
