using TheSqlODataMcp.Core;

namespace TheSqlODataMcp.Persistence;

/// <summary>
/// Identifies the control-store, identity-state, and catalog-import boundary.
/// </summary>
public static class PersistenceAssemblyMarker
{
    public static readonly string BoundaryName = "Persistence";

    public static readonly string CoreBoundaryName = CoreAssemblyMarker.BoundaryName;
}
