using TheSqlODataMcp.Core;

namespace TheSqlODataMcp.SqlServer;

/// <summary>
/// Identifies the SQL Server introspection, compilation, and type-mapping boundary.
/// </summary>
public static class SqlServerAssemblyMarker
{
    public static readonly string BoundaryName = "SqlServer";

    public static readonly string CoreBoundaryName = CoreAssemblyMarker.BoundaryName;
}
