using TheSqlODataMcp.Core.Catalog;

namespace TheSqlODataMcp.Persistence.CatalogStore;

/// <summary>
/// Ties <see cref="CatalogBootstrapPolicy"/>, <see cref="CatalogRevisionFactory"/>, and
/// <see cref="CatalogRevisionStore"/> together into the one startup decision the handoff's bootstrap
/// modes exist for: given the currently active revision (if any) and a freshly discovered
/// <see cref="TechnicalCatalog"/>, decide whether to build a new revision, persist it either way, and
/// activate it only if it succeeded. Introspecting the live source and loading the semantic overlay are
/// the caller's responsibility — this type stays provider-agnostic, matching
/// <see cref="CatalogRevisionFactory"/>.
/// </summary>
public sealed class CatalogBootstrapCoordinator
{
    private readonly CatalogRevisionStore store;

    public CatalogBootstrapCoordinator(CatalogRevisionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Returns the revision that should now be treated as active: the newly built-and-activated one if
    /// <paramref name="mode"/> called for a rebuild and it succeeded, otherwise the previously active
    /// revision unchanged (null if none has ever existed).
    /// </summary>
    public async Task<StoredCatalogRevision?> RunAsync(
        CatalogBootstrapMode mode,
        TechnicalCatalog technicalCatalog,
        SemanticOverlay? overlay,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(technicalCatalog);

        var active = await store.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        var freshHash = TechnicalCatalogCanonicalJson.ComputeStructuralHash(technicalCatalog);
        var changed = active is null || !string.Equals(active.TechnicalHash, freshHash, StringComparison.Ordinal);

        if (!CatalogBootstrapPolicy.ShouldImport(mode, active is not null, changed))
        {
            return active;
        }

        var revision = CatalogRevisionFactory.Create(technicalCatalog, overlay, now);
        var id = await store.SaveAsync(technicalCatalog, revision, cancellationToken).ConfigureAwait(false);

        if (!revision.Succeeded)
        {
            return active;
        }

        await store.ActivateAsync(id, now, cancellationToken).ConfigureAwait(false);
        return await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
    }
}
