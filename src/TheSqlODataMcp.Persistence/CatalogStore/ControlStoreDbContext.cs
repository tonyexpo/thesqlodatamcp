using Microsoft.EntityFrameworkCore;

namespace TheSqlODataMcp.Persistence.CatalogStore;

/// <summary>
/// EF Core access to the SQLite control store (see docs/architecture.md's "Persistence" section).
/// Currently hosts only <see cref="CatalogRevisions"/>; OpenIddict state, hashed approval tokens, and
/// admin audit are later, separate Milestone 5 work expected to extend this same context rather than
/// introduce a parallel one.
/// </summary>
public sealed class ControlStoreDbContext(DbContextOptions<ControlStoreDbContext> options) : DbContext(options)
{
    public DbSet<StoredCatalogRevision> CatalogRevisions => Set<StoredCatalogRevision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoredCatalogRevision>(entity =>
        {
            entity.ToTable("CatalogRevisions");
            entity.HasKey(revision => revision.Id);
            entity.Property(revision => revision.Status).HasConversion<string>().IsRequired();
            entity.Property(revision => revision.CreatedAt).IsRequired();
            entity.Property(revision => revision.TechnicalHash).IsRequired();
            entity.Property(revision => revision.TechnicalCatalogJson).IsRequired();
        });
    }
}
