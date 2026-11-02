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
                entity.Physical,
                entity.DisplayName,
                entity.Fields,
                entity.Aliases,
                entity.Description,
                exposed: true,
                odata: entity.OData,
                effectiveKeyFields: entity.EffectiveKeyFields,
                relationships: entity.Relationships.Where(relationship => exposedIdentities.Contains(relationship.Target))))
            .ToArray();

        Catalog = new MergedCatalog(
            catalog.CatalogVersion,
            catalog.Provider,
            catalog.Configured,
            entities,
            catalog.Name,
            catalog.Title,
            catalog.Description,
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
}
