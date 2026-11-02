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

        if (mode == CatalogBootstrapMode.Disabled)
        {
            return active;
        }

        // ImportIfChanged must compare the candidate revision's whole outcome, not only the technical hash: an
        // overlay-only edit (exposure, description, relationship) leaves the physical catalog identical but changes
        // what the gateway may expose (TS-03). AlwaysImport still needs a candidate to save.
        CatalogRevision? candidate = null;
        var changed = true;

        if (active is null || mode is CatalogBootstrapMode.ImportIfChanged or CatalogBootstrapMode.AlwaysImport)
        {
            candidate = CatalogRevisionFactory.Create(technicalCatalog, overlay, now);
            var candidateStored = CatalogRevisionMapper.ToStored(technicalCatalog, candidate);
            changed = active is null || !SameOutcome(active, candidateStored);
        }

        if (!CatalogBootstrapPolicy.ShouldImport(mode, active is not null, changed))
        {
            return active;
        }

        candidate ??= CatalogRevisionFactory.Create(technicalCatalog, overlay, now);
        var id = await store.SaveAsync(technicalCatalog, candidate, cancellationToken).ConfigureAwait(false);

        if (!candidate.Succeeded)
        {
            return active;
        }

        await store.ActivateAsync(id, now, cancellationToken).ConfigureAwait(false);
        return await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
    }

    private static bool SameOutcome(StoredCatalogRevision active, StoredCatalogRevision candidate) =>
        active.Status == candidate.Status
        && string.Equals(active.TechnicalHash, candidate.TechnicalHash, StringComparison.Ordinal)
        && string.Equals(active.MergedHash, candidate.MergedHash, StringComparison.Ordinal)
        && string.Equals(active.ValidationResultJson, candidate.ValidationResultJson, StringComparison.Ordinal);
}
