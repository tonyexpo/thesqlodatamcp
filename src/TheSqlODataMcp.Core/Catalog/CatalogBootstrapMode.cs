namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// Controls whether a new <see cref="CatalogRevision"/> is built and (if it succeeds) activated at
/// application startup. Named and valued exactly per the handoff's own four bootstrap modes
/// (<c>docs/AI_DATA_GATEWAY_HANDOFF.md</c>, "Control store" / "File policy").
/// </summary>
public enum CatalogBootstrapMode
{
    /// <summary>Never build a new revision at startup; keep serving whatever is already active.</summary>
    Disabled,

    /// <summary>Build a new revision only when no revision has ever been activated yet.</summary>
    ImportIfEmpty,

    /// <summary>Build a new revision when none is active yet, or when the technical catalog has changed.</summary>
    ImportIfChanged,

    /// <summary>Always build a new revision, whether or not the technical catalog has changed.</summary>
    AlwaysImport,
}

/// <summary>
/// The pure decision of whether a <see cref="CatalogBootstrapMode"/> calls for building a new
/// <see cref="CatalogRevision"/>, given only the two facts the decision can ever depend on. Kept separate
/// from any orchestration (introspection, persistence, activation) so the policy itself needs no I/O to test.
/// </summary>
public static class CatalogBootstrapPolicy
{
    public static bool ShouldImport(CatalogBootstrapMode mode, bool hasActiveRevision, bool technicalCatalogChanged) => mode switch
    {
        CatalogBootstrapMode.Disabled => false,
        CatalogBootstrapMode.ImportIfEmpty => !hasActiveRevision,
        CatalogBootstrapMode.ImportIfChanged => !hasActiveRevision || technicalCatalogChanged,
        CatalogBootstrapMode.AlwaysImport => true,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown catalog bootstrap mode."),
    };
}
