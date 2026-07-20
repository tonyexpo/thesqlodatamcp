using TheSqlODataMcp.Core;
using TheSqlODataMcp.Protocols;
using Xunit;

namespace TheSqlODataMcp.ProtocolTests;

public sealed class ProtocolBoundaryTests
{
    [Fact]
    public void ProtocolBoundaryDependsOnCoreOnly()
    {
        var references = typeof(ProtocolsAssemblyMarker).Assembly.GetReferencedAssemblies();

        Assert.Equal(CoreAssemblyMarker.BoundaryName, ProtocolsAssemblyMarker.CoreBoundaryName);
        Assert.Contains(references, reference => reference.Name == "TheSqlODataMcp.Core");
        Assert.DoesNotContain(
            references,
            reference => reference.Name is "TheSqlODataMcp.Persistence" or "TheSqlODataMcp.SqlServer" or "TheSqlODataMcp.Web");
    }
}
