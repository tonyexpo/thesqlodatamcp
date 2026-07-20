using TheSqlODataMcp.Core;

namespace TheSqlODataMcp.Protocols;

/// <summary>
/// Identifies protocol adapters that translate requests into the canonical query model.
/// </summary>
public static class ProtocolsAssemblyMarker
{
    public static readonly string BoundaryName = "Protocols";

    public static readonly string CoreBoundaryName = CoreAssemblyMarker.BoundaryName;
}
