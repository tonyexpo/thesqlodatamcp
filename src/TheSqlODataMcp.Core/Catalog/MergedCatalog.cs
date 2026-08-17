using System.Collections.ObjectModel;

namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// The result of merging a <see cref="TechnicalCatalog"/> with an optional <see cref="SemanticOverlay"/>
/// (see <see cref="CatalogMerger"/>). Every physical entity always appears in <see cref="Entities"/>,
/// annotated with whatever overlay data applies to it: the merge never adds or removes entities, and
/// it never filters based on <see cref="MergedEntity.Exposed"/> or <see cref="MergedEntity.OData"/> —
/// that protocol-level filtering belongs to a later, different slice (OData/MCP adapters). This type is
/// normally produced only by <see cref="CatalogMerger"/>, but follows the same construction-time
/// invariant discipline as <see cref="TechnicalCatalog"/> and <see cref="SemanticOverlay"/> so it remains
/// safe to construct directly.
/// </summary>
public sealed class MergedCatalog
{
    private readonly IReadOnlyList<MergedEntity> entities;
    private readonly IReadOnlyList<SemanticOverlayWarning> warnings;

    public MergedCatalog(
        string catalogVersion,
        string provider,
        bool configured,
        IEnumerable<MergedEntity> entities,
        string? name = null,
        string? title = null,
        string? description = null,
        IEnumerable<SemanticOverlayWarning>? warnings = null,
        string? markdown = null)
    {
        CatalogVersion = TechnicalCatalog.RequireIdentifier(catalogVersion, nameof(catalogVersion));
        Provider = TechnicalCatalog.RequireIdentifier(provider, nameof(provider));
        Configured = configured;
        this.entities = TechnicalCatalog.CopyDistinct(entities, static entity => entity.Physical.Identity, nameof(entities));
        Name = name;
        Title = title;
        Description = description;
        this.warnings = CopyWarnings(warnings ?? [], nameof(warnings));
        Markdown = markdown;
    }

    /// <summary>The technical catalog version this merge was produced from.</summary>
    public string CatalogVersion { get; }

    /// <summary>The provider name from the underlying <see cref="TechnicalCatalog"/>.</summary>
    public string Provider { get; }

    /// <summary>True iff a <see cref="SemanticOverlay"/> was supplied to the merge that produced this catalog.</summary>
    public bool Configured { get; }

    /// <summary>The overlay's name, or null when <see cref="Configured"/> is false.</summary>
    public string? Name { get; }

    /// <summary>The overlay's title, or null when <see cref="Configured"/> is false.</summary>
    public string? Title { get; }

    /// <summary>The overlay's description, or null when <see cref="Configured"/> is false.</summary>
    public string? Description { get; }

    /// <summary>The overlay's opaque Markdown narrative, or null when <see cref="Configured"/> is false.</summary>
    public string? Markdown { get; }

    /// <summary>The overlay's warnings, or empty when <see cref="Configured"/> is false.</summary>
    public IReadOnlyList<SemanticOverlayWarning> Warnings => warnings;

    /// <summary>Exactly one entry per entity in the underlying <see cref="TechnicalCatalog"/>.</summary>
    public IReadOnlyList<MergedEntity> Entities => entities;

    private static ReadOnlyCollection<SemanticOverlayWarning> CopyWarnings(IEnumerable<SemanticOverlayWarning> warnings, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(warnings, parameterName);
        var copy = warnings.ToArray();
        if (copy.Any(static warning => warning is null))
        {
            throw new ArgumentException("Collections cannot contain null values.", parameterName);
        }

        return new ReadOnlyCollection<SemanticOverlayWarning>(copy);
    }
}

/// <summary>
/// A single physical entity annotated with whatever semantic overlay data applies to it. Always present
/// for every entity in the underlying <see cref="TechnicalCatalog"/>, whether or not an overlay entity
/// matched it.
/// </summary>
public sealed class MergedEntity
{
    private readonly IReadOnlyList<string> aliases;
    private readonly IReadOnlyList<string> effectiveKeyFields;
    private readonly IReadOnlyList<MergedField> fields;
    private readonly IReadOnlyList<MergedRelationship> relationships;

    public MergedEntity(
        TechnicalEntity physical,
        string displayName,
        IEnumerable<MergedField> fields,
        IEnumerable<string>? aliases = null,
        string? description = null,
        bool exposed = true,
        SemanticOverlayODataSettings? odata = null,
        IEnumerable<string>? effectiveKeyFields = null,
        IEnumerable<MergedRelationship>? relationships = null)
    {
        ArgumentNullException.ThrowIfNull(physical);
        Physical = physical;
        DisplayName = TechnicalCatalog.RequireIdentifier(displayName, nameof(displayName));
        Description = description;
        this.aliases = SemanticOverlayCollections.CopyStrings(aliases, nameof(aliases));
        Exposed = exposed;
        OData = odata;
        this.effectiveKeyFields = SemanticOverlayCollections.CopyStrings(effectiveKeyFields, nameof(effectiveKeyFields));
        this.fields = TechnicalCatalog.CopyDistinct(fields, static field => field.Physical.Name, nameof(fields), StringComparer.Ordinal);
        this.relationships = TechnicalCatalog.CopyDistinct(
            relationships ?? [],
            static relationship => (relationship.Name, relationship.Provenance),
            nameof(relationships));

        ValidatePhysicalFieldReferences(this.effectiveKeyFields, nameof(effectiveKeyFields));
        ValidatePhysicalFieldReferences(this.fields.Select(static field => field.Physical.Name), nameof(fields));
        ValidatePhysicalFieldReferences(
            this.relationships.SelectMany(static relationship => relationship.FieldPairs.Select(pair => pair.SourceField)),
            nameof(relationships));
    }

    /// <summary>The untouched physical entity this merged entity annotates.</summary>
    public TechnicalEntity Physical { get; }

    /// <summary>
    /// The overlay's <c>displayName</c> if non-null/non-empty, else the overlay's <c>name</c> if provided,
    /// else <see cref="PhysicalObjectIdentity.ObjectName"/>. Always non-null.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>The overlay's description if present, else the physical entity's description.</summary>
    public string? Description { get; }

    /// <summary>The overlay's aliases, or empty if none were declared.</summary>
    public IReadOnlyList<string> Aliases => aliases;

    /// <summary>The overlay's explicit exposed value if set, else true (default-visible).</summary>
    public bool Exposed { get; }

    /// <summary>Passthrough of the overlay entity's OData settings, or null if none were declared.</summary>
    public SemanticOverlayODataSettings? OData { get; }

    /// <summary>
    /// The effective logical key: the overlay's <c>odata.key</c> when declared (an explicit override that
    /// always wins), else the physical primary key's fields, else empty for a keyless entity.
    /// </summary>
    public IReadOnlyList<string> EffectiveKeyFields => effectiveKeyFields;

    /// <summary>Exactly one entry per field on <see cref="Physical"/>.</summary>
    public IReadOnlyList<MergedField> Fields => fields;

    /// <summary>FK-discovered relationships plus overlay-declared relationships, as a union.</summary>
    public IReadOnlyList<MergedRelationship> Relationships => relationships;

    private void ValidatePhysicalFieldReferences(IEnumerable<string> names, string parameterName)
    {
        var physicalFieldNames = new HashSet<string>(Physical.Fields.Select(static field => field.Name), StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (!physicalFieldNames.Contains(name))
            {
                throw new ArgumentException($"The field '{name}' is not present on entity '{Physical.Identity}'.", parameterName);
            }
        }
    }
}

/// <summary>A single physical field annotated with whatever semantic overlay field data applies to it.</summary>
public sealed class MergedField
{
    private readonly IReadOnlyList<string> aliases;

    public MergedField(
        TechnicalField physical,
        string? displayName = null,
        string? description = null,
        IEnumerable<string>? aliases = null)
    {
        ArgumentNullException.ThrowIfNull(physical);
        Physical = physical;
        DisplayName = displayName;
        Description = description;
        this.aliases = SemanticOverlayCollections.CopyStrings(aliases, nameof(aliases));
    }

    /// <summary>The untouched physical field this merged field annotates.</summary>
    public TechnicalField Physical { get; }

    /// <summary>The overlay field's display name, or null if none was declared.</summary>
    public string? DisplayName { get; }

    /// <summary>The overlay field's description if present, else the physical field's description.</summary>
    public string? Description { get; }

    /// <summary>The overlay field's aliases, or empty if none were declared.</summary>
    public IReadOnlyList<string> Aliases => aliases;
}

/// <summary>Where a <see cref="MergedRelationship"/> came from.</summary>
public enum RelationshipProvenance
{
    /// <summary>Discovered from a foreign key during physical catalog introspection.</summary>
    Discovered,

    /// <summary>Declared by an administrator in the semantic overlay YAML.</summary>
    Configured,
}

/// <summary>
/// A relationship in the merged view: either FK-discovered (<see cref="RelationshipProvenance.Discovered"/>)
/// or overlay-declared (<see cref="RelationshipProvenance.Configured"/>). The merged relationship list for
/// an entity is a union of both, never a replacement.
/// </summary>
public sealed class MergedRelationship
{
    private readonly IReadOnlyList<RelationshipFieldPair> fieldPairs;

    public MergedRelationship(
        string name,
        PhysicalObjectIdentity target,
        IEnumerable<RelationshipFieldPair> fieldPairs,
        RelationshipProvenance provenance,
        SemanticOverlayCardinality? cardinality = null,
        string? description = null)
    {
        Name = TechnicalCatalog.RequireIdentifier(name, nameof(name));
        ArgumentNullException.ThrowIfNull(target);
        Target = target;
        this.fieldPairs = CopyFieldPairs(fieldPairs);
        if (!Enum.IsDefined(provenance))
        {
            throw new ArgumentOutOfRangeException(nameof(provenance), "The relationship provenance is not supported.");
        }

        Provenance = provenance;
        if (cardinality is not null && !Enum.IsDefined(cardinality.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(cardinality), "The relationship cardinality is not supported.");
        }

        Cardinality = cardinality;
        Description = description;
    }

    /// <summary>The FK constraint name for <see cref="RelationshipProvenance.Discovered"/>, else the YAML relationship key.</summary>
    public string Name { get; }

    public PhysicalObjectIdentity Target { get; }

    public IReadOnlyList<RelationshipFieldPair> FieldPairs => fieldPairs;

    public RelationshipProvenance Provenance { get; }

    /// <summary>Null for <see cref="RelationshipProvenance.Discovered"/>; the overlay's value for <see cref="RelationshipProvenance.Configured"/>.</summary>
    public SemanticOverlayCardinality? Cardinality { get; }

    /// <summary>The underlying <see cref="CatalogRelationship"/> description for Discovered, the overlay relationship's description for Configured.</summary>
    public string? Description { get; }

    private static ReadOnlyCollection<RelationshipFieldPair> CopyFieldPairs(IEnumerable<RelationshipFieldPair> fieldPairs)
    {
        ArgumentNullException.ThrowIfNull(fieldPairs, nameof(fieldPairs));
        var copy = fieldPairs.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("At least one relationship field pair is required.", nameof(fieldPairs));
        }

        if (copy.Any(static pair => pair is null))
        {
            throw new ArgumentException("Collections cannot contain null values.", nameof(fieldPairs));
        }

        var seenPairs = new HashSet<(string Source, string Target)>();
        foreach (var pair in copy)
        {
            if (!seenPairs.Add((pair.SourceField, pair.TargetField)))
            {
                throw new ArgumentException("Relationship field pairs cannot be duplicated.", nameof(fieldPairs));
            }
        }

        return new ReadOnlyCollection<RelationshipFieldPair>(copy);
    }
}
