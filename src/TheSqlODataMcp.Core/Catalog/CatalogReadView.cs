namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// The only catalog view a protocol adapter may expose to a caller. It keeps the internal
/// <see cref="MergedCatalog"/> intact for administration/introspection, but filters it to entities whose effective
/// <see cref="MergedEntity.Exposed"/> is true, drops relationships whose target is hidden or absent, and does not
/// carry the catalog-level narrative/warnings (which may mention hidden objects). Before Milestone 3/4, every JSON,
/// OData and MCP catalog/query surface must resolve metadata through this view (TS-01).
/// </summary>
public sealed class CatalogReadView
{
    private readonly CatalogSearchIndex searchIndex;

    public CatalogReadView(MergedCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var exposed = catalog.Entities.Where(static entity => entity.Exposed).ToArray();
        var exposedIdentities = exposed
            .Select(static entity => entity.Physical.Identity)
            .ToHashSet();

        var entities = exposed
            .Select(entity => new MergedEntity(
                SanitizePhysical(entity.Physical, exposedIdentities),
                entity.DisplayName,
                entity.Fields,
                entity.Aliases,
                entity.Description,
                exposed: true,
                odata: entity.OData,
                effectiveKeyFields: entity.EffectiveKeyFields,
                relationships: entity.Relationships.Where(relationship => exposedIdentities.Contains(relationship.Target))))
            .ToArray();

        // Catalog-level narrative is omitted too: a description or title written for administrators can name a
        // hidden object just like warnings and Markdown can (TS3-02).
        Catalog = new MergedCatalog(
            catalog.CatalogVersion,
            catalog.Provider,
            catalog.Configured,
            entities,
            name: null,
            title: null,
            description: null,
            warnings: [],
            markdown: null);

        searchIndex = new CatalogSearchIndex(Catalog);
    }

    /// <summary>The filtered catalog. It contains only exposed entities and relationships between them.</summary>
    public MergedCatalog Catalog { get; }

    /// <summary>Finds an exposed entity by physical identity, or null when it is hidden or absent.</summary>
    public MergedEntity? FindEntity(PhysicalObjectIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return searchIndex.FindEntity(identity);
    }

    /// <summary>Ranked search restricted to the exposed view.</summary>
    public IReadOnlyList<CatalogSearchMatch> Search(string query, int maxResults = 20) =>
        searchIndex.Search(query, maxResults);

    /// <summary>
    /// Rebuilds the physical entity with only the relationships whose target is exposed. The merged relationship
    /// list is filtered separately; this closes the underlying physical graph that a default object serializer or
    /// a direct <see cref="MergedEntity.Physical"/> reader would otherwise expose (TS3-02).
    /// </summary>
    private static TechnicalEntity SanitizePhysical(
        TechnicalEntity physical,
        HashSet<PhysicalObjectIdentity> exposedIdentities)
        => new(
            physical.Identity,
            physical.Kind,
            physical.Fields,
            physical.Keys,
            physical.Indexes,
            physical.Relationships.Where(relationship => exposedIdentities.Contains(relationship.Target)),
            physical.Description,
            physical.IsTemporal);
}
