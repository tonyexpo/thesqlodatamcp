using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TheSqlODataMcp.Persistence.CatalogStore;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> construct <see cref="ControlStoreDbContext"/> without a hosted
/// app. The connection string here is never used at runtime — see <c>ControlStore:ConnectionString</c>
/// in TheSqlODataMcp.Web's configuration for the real one.
/// </summary>
public sealed class ControlStoreDbContextFactory : IDesignTimeDbContextFactory<ControlStoreDbContext>
{
    public ControlStoreDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ControlStoreDbContext>();
        optionsBuilder.UseSqlite("Data Source=design-time-only.db");
        return new ControlStoreDbContext(optionsBuilder.Options);
    }
}
