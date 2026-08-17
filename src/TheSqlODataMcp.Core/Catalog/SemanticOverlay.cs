using System.Collections.ObjectModel;

namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// The validated, typed result of importing an administrator-authored semantic overlay
/// (Markdown narrative plus YAML metadata) against a previously discovered
/// <see cref="TechnicalCatalog"/>. This is the isolated import/validation slice only: it does not
/// merge the overlay into a <see cref="TechnicalCatalog"/>, which is a separate future slice.
/// </summary>
public sealed class SemanticOverlay
{
    private readonly IReadOnlyList<SemanticOverlayEntity> entities;
    private readonly IReadOnlyList<SemanticOverlayWarning> warnings;

    public SemanticOverlay(
        string catalogVersion,
        IEnumerable<SemanticOverlayEntity> entities,
        string? name = null,
        string? title = null,
        string? description = null,
        IEnumerable<SemanticOverlayWarning>? warnings = null,
        string? markdown = null)
    {
        CatalogVersion = TechnicalCatalog.RequireIdentifier(catalogVersion, nameof(catalogVersion));
        this.entities = TechnicalCatalog.CopyDistinct(entities, static entity => entity.Source, nameof(entities));
        Name = name;
        Title = title;
        Description = description;
        this.warnings = CopyWarnings(warnings ?? [], nameof(warnings));
        Markdown = markdown ?? string.Empty;
    }

    public string CatalogVersion { get; }

    public string? Name { get; }

    public string? Title { get; }

    public string? Description { get; }

    public IReadOnlyList<SemanticOverlayEntity> Entities => entities;

    public IReadOnlyList<SemanticOverlayWarning> Warnings => warnings;

    /// <summary>
    /// The opaque, administrator-authored Markdown narrative. It is returned verbatim to callers
    /// and is never parsed into structured rules.
    /// </summary>
    public string Markdown { get; }

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
/// An overlay entry for a single physical entity, addressed by its stable <c>schema.object</c> source.
/// </summary>
public sealed class SemanticOverlayEntity
{
    private readonly IReadOnlyList<string> aliases;
    private readonly IReadOnlyDictionary<string, SemanticOverlayField> fields;
    private readonly IReadOnlyDictionary<string, SemanticOverlayRelationship> relationships;

    public SemanticOverlayEntity(
        PhysicalObjectIdentity source,
        string? name = null,
        string? displayName = null,
        string? description = null,
        IEnumerable<string>? aliases = null,
        bool? exposed = null,
        SemanticOverlayODataSettings? odata = null,
        IEnumerable<KeyValuePair<string, SemanticOverlayField>>? fields = null,
        IEnumerable<KeyValuePair<string, SemanticOverlayRelationship>>? relationships = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
        Name = name;
        DisplayName = displayName;
        Description = description;
        this.aliases = SemanticOverlayCollections.CopyStrings(aliases, nameof(aliases));
        Exposed = exposed;
        OData = odata;
        this.fields = SemanticOverlayCollections.CopyDistinctMap(fields, nameof(fields));
        this.relationships = SemanticOverlayCollections.CopyDistinctMap(relationships, nameof(relationships));
    }

    public PhysicalObjectIdentity Source { get; }

    public string? Name { get; }

    public string? DisplayName { get; }

    public string? Description { get; }

    public IReadOnlyList<string> Aliases => aliases;

    public bool? Exposed { get; }

    public SemanticOverlayODataSettings? OData { get; }

    public IReadOnlyDictionary<string, SemanticOverlayField> Fields => fields;

    public IReadOnlyDictionary<string, SemanticOverlayRelationship> Relationships => relationships;
}

/// <summary>
/// Overlay metadata applied to a single physical field, keyed externally by the physical column name.
/// </summary>
public sealed class SemanticOverlayField
{
    private readonly IReadOnlyList<string> aliases;

    public SemanticOverlayField(
        string? name = null,
        string? displayName = null,
        string? description = null,
        IEnumerable<string>? aliases = null)
    {
        Name = name;
        DisplayName = displayName;
        Description = description;
        this.aliases = SemanticOverlayCollections.CopyStrings(aliases, nameof(aliases));
    }

    public string? Name { get; }

    public string? DisplayName { get; }

    public string? Description { get; }

    public IReadOnlyList<string> Aliases => aliases;
}

/// <summary>
/// Overlay-declared OData exposure preferences for an entity.
/// </summary>
public sealed class SemanticOverlayODataSettings
{
    private readonly IReadOnlyList<string> key;

    public SemanticOverlayODataSettings(bool? enabled = null, string? entitySetName = null, IEnumerable<string>? key = null)
    {
        Enabled = enabled;
        EntitySetName = entitySetName;
        this.key = SemanticOverlayCollections.CopyStrings(key, nameof(key));
    }

    public bool? Enabled { get; }

    public string? EntitySetName { get; }

    public IReadOnlyList<string> Key => key;
}

public enum SemanticOverlayCardinality
{
    OneToOne,
    OneToMany,
    ManyToOne,
    ManyToMany,
}

/// <summary>
/// One <c>sourceField</c>/<c>targetField</c> join pair belonging to an overlay relationship.
/// </summary>
public sealed class SemanticOverlayJoinFieldPair
{
    public SemanticOverlayJoinFieldPair(string sourceField, string targetField)
    {
        SourceField = TechnicalCatalog.RequireIdentifier(sourceField, nameof(sourceField));
        TargetField = TechnicalCatalog.RequireIdentifier(targetField, nameof(targetField));
    }

    public string SourceField { get; }

    public string TargetField { get; }
}

/// <summary>
/// An overlay-declared relationship, keyed externally by the administrator-chosen relationship name.
/// </summary>
public sealed class SemanticOverlayRelationship
{
    private readonly IReadOnlyList<SemanticOverlayJoinFieldPair> join;

    public SemanticOverlayRelationship(
        PhysicalObjectIdentity target,
        IEnumerable<SemanticOverlayJoinFieldPair> join,
        SemanticOverlayCardinality? cardinality = null,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        Target = target;
        this.join = CopyJoin(join, nameof(join));
        Cardinality = cardinality;
        Description = description;
    }

    public PhysicalObjectIdentity Target { get; }

    public IReadOnlyList<SemanticOverlayJoinFieldPair> Join => join;

    public SemanticOverlayCardinality? Cardinality { get; }

    public string? Description { get; }

    private static ReadOnlyCollection<SemanticOverlayJoinFieldPair> CopyJoin(IEnumerable<SemanticOverlayJoinFieldPair> join, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(join, parameterName);
        var copy = join.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("At least one join field pair is required.", parameterName);
        }

        if (copy.Any(static pair => pair is null))
        {
            throw new ArgumentException("Collections cannot contain null values.", parameterName);
        }

        return new ReadOnlyCollection<SemanticOverlayJoinFieldPair>(copy);
    }
}

/// <summary>
/// An administrator-authored warning surfaced alongside the catalog.
/// </summary>
public sealed class SemanticOverlayWarning
{
    public SemanticOverlayWarning(string title, string content)
    {
        Title = TechnicalCatalog.RequireIdentifier(title, nameof(title));
        Content = TechnicalCatalog.RequireIdentifier(content, nameof(content));
    }

    public string Title { get; }

    public string Content { get; }
}

/// <summary>
/// Small shared collection-copy helpers for the semantic overlay domain model, mirroring
/// <see cref="TechnicalCatalog"/>'s defensive-copy discipline for the map-shaped overlay collections
/// (<c>fields</c>/<c>relationships</c>) that <see cref="TechnicalCatalog.CopyDistinct{T, TKey}"/> does not shape for.
/// </summary>
internal static class SemanticOverlayCollections
{
    public static IReadOnlyList<string> CopyStrings(IEnumerable<string>? values, string parameterName)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        var copy = values.ToArray();
        if (copy.Any(static value => value is null))
        {
            throw new ArgumentException("Collections cannot contain null values.", parameterName);
        }

        return new ReadOnlyCollection<string>(copy);
    }

    public static IReadOnlyDictionary<string, TValue> CopyDistinctMap<TValue>(
        IEnumerable<KeyValuePair<string, TValue>>? entries,
        string parameterName)
        where TValue : notnull
    {
        var map = new Dictionary<string, TValue>(StringComparer.Ordinal);
        if (entries is null)
        {
            return new ReadOnlyDictionary<string, TValue>(map);
        }

        foreach (var entry in entries)
        {
            if (entry.Key is null)
            {
                throw new ArgumentException("Map keys cannot be null.", parameterName);
            }

            if (entry.Value is null)
            {
                throw new ArgumentException("Map values cannot be null.", parameterName);
            }

            if (!map.TryAdd(entry.Key, entry.Value))
            {
                throw new ArgumentException($"The key '{entry.Key}' is duplicated.", parameterName);
            }
        }

        return new ReadOnlyDictionary<string, TValue>(map);
    }
}
