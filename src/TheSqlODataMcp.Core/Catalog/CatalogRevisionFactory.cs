namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// Builds a <see cref="CatalogRevision"/> from a discovered <see cref="TechnicalCatalog"/> and an optional
/// <see cref="SemanticOverlay"/>, by running <see cref="CatalogMerger.Merge"/> and computing both structural
/// hashes. This is the first production consumer of <see cref="CatalogRevision"/>, mirroring how
/// <see cref="CatalogMerger"/> was the first production consumer of <see cref="Catalog.SemanticOverlay"/>.
/// </summary>
public static class CatalogRevisionFactory
{
    public static CatalogRevision Create(TechnicalCatalog catalog, SemanticOverlay? overlay, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var technicalHash = TechnicalCatalogCanonicalJson.ComputeStructuralHash(catalog);
        var mergeResult = CatalogMerger.Merge(catalog, overlay);

        if (!mergeResult.Succeeded)
        {
            return CatalogRevision.Failure(createdAt, technicalHash, mergeResult.Errors);
        }

        var mergedHash = MergedCatalogCanonicalJson.ComputeStructuralHash(mergeResult.MergedCatalog!);
        return CatalogRevision.Success(createdAt, technicalHash, mergeResult.MergedCatalog!, mergedHash);
    }
}
