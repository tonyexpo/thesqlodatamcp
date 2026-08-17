using System.Collections.ObjectModel;

namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// Stable machine-readable codes for every problem <see cref="CatalogMerger"/> can detect while
/// re-validating a <see cref="SemanticOverlay"/> against a specific <see cref="TechnicalCatalog"/>.
/// </summary>
public static class CatalogMergeErrorCodes
{
    /// <summary>The overlay's <c>catalogVersion</c> does not exactly (ordinal) match the technical catalog's version.</summary>
    public const string CatalogVersionMismatch = "merge.catalogVersionMismatch";

    /// <summary>An overlay entity's <c>source</c> does not resolve to a physical entity in the supplied catalog.</summary>
    public const string EntitySourceNotFound = "merge.entitySourceNotFound";

    /// <summary>An overlay entity's <c>fields</c> map key does not resolve to a field on the resolved physical entity.</summary>
    public const string FieldNotFound = "merge.fieldNotFound";

    /// <summary>An overlay relationship's <c>target</c> does not resolve to a physical entity in the supplied catalog.</summary>
    public const string RelationshipTargetNotFound = "merge.relationshipTargetNotFound";

    /// <summary>A join pair's <c>sourceField</c> does not resolve to a field on the relationship's source entity.</summary>
    public const string JoinSourceFieldNotFound = "merge.joinSourceFieldNotFound";

    /// <summary>A join pair's <c>targetField</c> does not resolve to a field on the relationship's target entity.</summary>
    public const string JoinTargetFieldNotFound = "merge.joinTargetFieldNotFound";

    /// <summary>An overlay entity's <c>odata.key</c> entry does not resolve to a field on the resolved physical entity.</summary>
    public const string ODataKeyFieldNotFound = "merge.oDataKeyFieldNotFound";

    /// <summary>
    /// An overlay relationship's YAML key (its name) is empty or whitespace-only. Slice 4A's schema does
    /// not forbid this at import time because relationship map keys are unconstrained property names; it
    /// must be rejected here because a whitespace-only name cannot become a valid <see cref="MergedRelationship.Name"/>.
    /// </summary>
    public const string RelationshipNameInvalid = "merge.relationshipNameInvalid";
}

/// <summary>
/// The outcome of a <see cref="CatalogMerger.Merge"/> call: either the merged <see cref="Catalog.MergedCatalog"/>,
/// or every independently detectable validation error collected in one pass. Mirrors
/// <see cref="SemanticOverlayImportResult"/>'s shape and reuses <see cref="SemanticOverlayValidationError"/>
/// rather than defining a new, identically-shaped error type.
/// </summary>
public sealed class CatalogMergeResult
{
    private readonly IReadOnlyList<SemanticOverlayValidationError> errors;

    private CatalogMergeResult(MergedCatalog? mergedCatalog, IReadOnlyList<SemanticOverlayValidationError> errors)
    {
        MergedCatalog = mergedCatalog;
        this.errors = errors;
    }

    /// <summary>True when the merge produced a valid <see cref="Catalog.MergedCatalog"/> with no errors.</summary>
    public bool Succeeded => MergedCatalog is not null;

    /// <summary>The merged catalog when <see cref="Succeeded"/> is true; otherwise null.</summary>
    public MergedCatalog? MergedCatalog { get; }

    /// <summary>Every validation error detected, in detection order. Empty when <see cref="Succeeded"/> is true.</summary>
    public IReadOnlyList<SemanticOverlayValidationError> Errors => errors;

    public static CatalogMergeResult Success(MergedCatalog mergedCatalog)
    {
        ArgumentNullException.ThrowIfNull(mergedCatalog);
        return new CatalogMergeResult(mergedCatalog, Array.Empty<SemanticOverlayValidationError>());
    }

    public static CatalogMergeResult Failure(IEnumerable<SemanticOverlayValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var copy = new ReadOnlyCollection<SemanticOverlayValidationError>(errors.ToArray());
        if (copy.Count == 0)
        {
            throw new ArgumentException("A failure result requires at least one validation error.", nameof(errors));
        }

        return new CatalogMergeResult(null, copy);
    }
}
