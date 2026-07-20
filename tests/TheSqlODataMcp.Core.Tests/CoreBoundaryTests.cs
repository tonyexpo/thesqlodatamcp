using TheSqlODataMcp.Core;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

public sealed class CoreBoundaryTests
{
    [Fact]
    public void CoreIsProviderNeutralAndHasNoProductProjectReferences()
    {
        var references = typeof(CoreAssemblyMarker).Assembly.GetReferencedAssemblies();

        Assert.Equal("Core", CoreAssemblyMarker.BoundaryName);
        Assert.DoesNotContain(
            references,
            reference => reference.Name?.StartsWith("TheSqlODataMcp.", StringComparison.Ordinal) == true);
    }
}
