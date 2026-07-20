using TheSqlODataMcp.Core;
using TheSqlODataMcp.Persistence;
using TheSqlODataMcp.Protocols;
using TheSqlODataMcp.SqlServer;

namespace TheSqlODataMcp.Web;

/// <summary>
/// Identifies the ASP.NET Core composition-root boundary.
/// </summary>
public static class WebAssemblyMarker
{
    public static readonly string[] ComposedBoundaryNames =
    [
        CoreAssemblyMarker.BoundaryName,
        SqlServerAssemblyMarker.BoundaryName,
        PersistenceAssemblyMarker.BoundaryName,
        ProtocolsAssemblyMarker.BoundaryName,
    ];
}
