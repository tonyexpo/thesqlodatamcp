using Microsoft.EntityFrameworkCore;
using TheSqlODataMcp.Core.Catalog;

namespace TheSqlODataMcp.Persistence.CatalogStore;

/// <summary>
/// Durable storage for <see cref="CatalogRevision"/> build attempts. Stores every attempt, succeeded or
/// failed, as an immutable, append-only row. Reconstructing a live <see cref="MergedCatalog"/> object
/// graph from a stored row, tracking which revision is currently active, and rollback are later Milestone 1
/// work (activation) built on top of this store, not part of it — mirroring ADR 0012's own scope discipline.
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
}
