using TheSqlODataMcp.Core;
using TheSqlODataMcp.SqlServer;
using Xunit;

namespace TheSqlODataMcp.SqlServer.Tests;

public sealed class SqlServerBoundaryTests
{
    [Fact]
    public void SqlServerBoundaryDependsOnCoreOnly()
    {
        var references = typeof(SqlServerAssemblyMarker).Assembly.GetReferencedAssemblies();

        Assert.Equal(CoreAssemblyMarker.BoundaryName, SqlServerAssemblyMarker.CoreBoundaryName);
        Assert.Contains(references, reference => reference.Name == "TheSqlODataMcp.Core");
        Assert.DoesNotContain(
            references,
            reference => reference.Name is "TheSqlODataMcp.Persistence" or "TheSqlODataMcp.Protocols" or "TheSqlODataMcp.Web");
    }
}
