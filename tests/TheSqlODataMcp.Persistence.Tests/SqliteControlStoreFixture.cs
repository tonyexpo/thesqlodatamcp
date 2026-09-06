using Microsoft.EntityFrameworkCore;
using TheSqlODataMcp.Persistence.CatalogStore;

namespace TheSqlODataMcp.Persistence.Tests;

/// <summary>
/// A real, file-backed disposable SQLite database for one test, mirroring
/// TheSqlODataMcp.IntegrationTests's SqlServerReportingCatalogFixture pattern — a private constructor
/// plus a static async factory, disposal that deletes the file, and no shared/injected xunit fixture
/// lifetime. Unlike SQL Server, SQLite needs no Docker/Testcontainers, so tests using this fixture carry
/// no <c>[Trait("Category", ...)]</c> and run inside the normal <c>validate</c> CI job.
/// </summary>
internal sealed class SqliteControlStoreFixture : IAsyncDisposable
{
    private SqliteControlStoreFixture(string databasePath, ControlStoreDbContext dbContext)
    {
        DatabasePath = databasePath;
        DbContext = dbContext;
    }

    internal string DatabasePath { get; }

    internal ControlStoreDbContext DbContext { get; }

    internal static async Task<SqliteControlStoreFixture> CreateAsync(CancellationToken cancellationToken)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"thesqlodatamcp-controlstore-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<ControlStoreDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var dbContext = new ControlStoreDbContext(options);
        await dbContext.Database.MigrateAsync(cancellationToken);
        return new SqliteControlStoreFixture(databasePath, dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.Database.CloseConnectionAsync();
        await DbContext.DisposeAsync();
        if (File.Exists(DatabasePath))
        {
            File.Delete(DatabasePath);
        }
    }
}
