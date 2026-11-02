using Microsoft.EntityFrameworkCore;
using TheSqlODataMcp.Core.Catalog;

namespace TheSqlODataMcp.Persistence.CatalogStore;

/// <summary>
/// Durable storage for <see cref="CatalogRevision"/> build attempts. Stores every attempt, succeeded or
/// failed, as an immutable, append-only row, plus (per ADR 0014) which one is currently active. This type
/// only ever deals in the raw JSON-blob rows themselves; reconstructing a live <see cref="MergedCatalog"/>
/// object graph from one is <see cref="CatalogRevisionMapper.ToDomain"/>'s job (ADR 0015), not this store's.
/// </summary>
public sealed class CatalogRevisionStore
{
    private readonly ControlStoreDbContext dbContext;

    public CatalogRevisionStore(ControlStoreDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        this.dbContext = dbContext;
    }

    /// <summary>Persists one build attempt and returns its database-generated <see cref="StoredCatalogRevision.Id"/>.</summary>
    public async Task<long> SaveAsync(TechnicalCatalog technicalCatalog, CatalogRevision revision, CancellationToken cancellationToken)
    {
        var stored = CatalogRevisionMapper.ToStored(technicalCatalog, revision);
        dbContext.CatalogRevisions.Add(stored);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return stored.Id;
    }

    /// <summary>Reads back one stored row by id, or null if no such revision exists.</summary>
    public Task<StoredCatalogRevision?> GetAsync(long id, CancellationToken cancellationToken) =>
        dbContext.CatalogRevisions
            .AsNoTracking()
            .SingleOrDefaultAsync(revision => revision.Id == id, cancellationToken);

    /// <summary>
    /// Reads back the currently active revision — the row with the greatest non-null
    /// <see cref="StoredCatalogRevision.ActivatedAt"/> — or null if no revision has ever been activated.
    /// Only the id and timestamp are loaded for the selection; the full row (including the catalog JSON blobs)
    /// is loaded once, after the winner is known. This keeps historical revisions out of memory even when
    /// <c>AlwaysImport</c> has accumulated many of them.
    /// </summary>
    public async Task<StoredCatalogRevision?> GetActiveAsync(CancellationToken cancellationToken)
    {
        var activated = await dbContext.CatalogRevisions
            .AsNoTracking()
            .Where(revision => revision.ActivatedAt != null)
            .Select(revision => new { revision.Id, revision.ActivatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // SQLite's EF Core provider cannot translate ORDER BY over DateTimeOffset; the projection above makes
        // the client-side sort cheap because no catalog JSON is materialized.
        var activeId = activated
            .OrderByDescending(revision => revision.ActivatedAt)
            .ThenByDescending(revision => revision.Id)
            .Select(revision => (long?)revision.Id)
            .FirstOrDefault();

        return activeId is null ? null : await GetAsync(activeId.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Atomically marks revision <paramref name="id"/> as active as of <paramref name="activatedAt"/> — a
    /// single tracked-entity update through one <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>
    /// call. Activation timestamps must strictly increase: a clock rollback is rejected instead of producing a
    /// row that <see cref="GetActiveAsync"/> would not select. Throws <see cref="ArgumentException"/> if no such
    /// revision exists, <see cref="InvalidOperationException"/> if the revision failed or the timestamp is not
    /// monotonically increasing — a failed revision or a rejected activation never disturbs the active row.
    /// </summary>
    public async Task ActivateAsync(long id, DateTimeOffset activatedAt, CancellationToken cancellationToken)
    {
        var revision = await dbContext.CatalogRevisions.SingleOrDefaultAsync(revision => revision.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new ArgumentException($"No catalog revision with id {id} exists.", nameof(id));

        var active = await dbContext.CatalogRevisions
            .AsNoTracking()
            .Where(revision => revision.ActivatedAt != null)
            .Select(revision => new { revision.Id, revision.ActivatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var current = active
            .OrderByDescending(revision => revision.ActivatedAt)
            .ThenByDescending(revision => revision.Id)
            .FirstOrDefault();

        if (current is not null
            && current.ActivatedAt is { } currentActivatedAt
            && (activatedAt < currentActivatedAt
                || (activatedAt == currentActivatedAt && id <= current.Id)))
        {
            throw new InvalidOperationException(
                $"Catalog revision {id} cannot be activated at {activatedAt:O}: the active revision "
                + $"{current.Id} is already active at {currentActivatedAt:O}. Activation timestamps must increase "
                + "monotonically, so a clock rollback cannot create a revision that GetActiveAsync would not select.");
        }

        revision.Activate(activatedAt);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
