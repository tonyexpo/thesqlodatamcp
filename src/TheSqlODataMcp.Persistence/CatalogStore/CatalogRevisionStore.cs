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
    /// Ordering happens client-side: SQLite's EF Core provider cannot translate an ORDER BY over
    /// <see cref="DateTimeOffset"/> into SQL, and the set of ever-activated rows is small in practice.
    /// </summary>
    public async Task<StoredCatalogRevision?> GetActiveAsync(CancellationToken cancellationToken)
    {
        var activated = await dbContext.CatalogRevisions
            .AsNoTracking()
            .Where(revision => revision.ActivatedAt != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return activated
            .OrderByDescending(revision => revision.ActivatedAt)
            .ThenByDescending(revision => revision.Id)
            .FirstOrDefault();
    }

    /// <summary>
    /// Atomically marks revision <paramref name="id"/> as active as of <paramref name="activatedAt"/> — a
    /// single tracked-entity update through one <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>
    /// call. Throws <see cref="ArgumentException"/> if no such revision exists, or
    /// <see cref="InvalidOperationException"/> (via <see cref="StoredCatalogRevision.Activate"/>) if it
    /// failed — a failed revision can never become active, which is what makes last-valid rollback
    /// automatic: the caller simply never reaches a state where the previously active row is disturbed.
    /// </summary>
    public async Task ActivateAsync(long id, DateTimeOffset activatedAt, CancellationToken cancellationToken)
    {
        var revision = await dbContext.CatalogRevisions.SingleOrDefaultAsync(revision => revision.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new ArgumentException($"No catalog revision with id {id} exists.", nameof(id));

        revision.Activate(activatedAt);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
