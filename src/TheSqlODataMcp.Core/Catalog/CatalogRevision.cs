using System.Collections.ObjectModel;

namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// The outcome of one attempt to build a catalog at a point in time: either it succeeded, producing a
/// usable <see cref="Catalog.MergedCatalog"/>, or it failed, producing collected validation errors.
/// </summary>
public enum CatalogRevisionStatus
{
    Succeeded,
    Failed,
}

/// <summary>
/// An immutable snapshot of one attempt to build a catalog from a discovered <see cref="TechnicalCatalog"/>
/// and an optional <see cref="SemanticOverlay"/>, timestamped and content-addressed by structural hash.
/// This models only the outcome of a single build attempt — it does not track which revision is currently
/// serving, supersede an earlier revision, or persist anything; multi-revision activation and rollback are
/// later Milestone 1 work built on top of this type, not part of it.
/// </summary>
public sealed class CatalogRevision
{
    private readonly IReadOnlyList<SemanticOverlayValidationError> errors;

    private CatalogRevision(
        DateTimeOffset createdAt,
        CatalogRevisionStatus status,
        string technicalHash,
        MergedCatalog? mergedCatalog,
        string? mergedHash,
        IReadOnlyList<SemanticOverlayValidationError> errors)
    {
        CreatedAt = createdAt;
        Status = status;
        TechnicalHash = technicalHash;
        MergedCatalog = mergedCatalog;
        MergedHash = mergedHash;
        this.errors = errors;
    }

    /// <summary>When this revision was built. Not part of any structural hash.</summary>
    public DateTimeOffset CreatedAt { get; }

    public CatalogRevisionStatus Status { get; }

    /// <summary>True iff <see cref="Status"/> is <see cref="CatalogRevisionStatus.Succeeded"/>.</summary>
    public bool Succeeded => Status == CatalogRevisionStatus.Succeeded;

    /// <summary>
    /// The structural hash of the source <see cref="TechnicalCatalog"/> (<see cref="TechnicalCatalogCanonicalJson.ComputeStructuralHash"/>).
    /// Always present, independent of whether the merge succeeded, since technical discovery happens before merging.
    /// </summary>
    public string TechnicalHash { get; }

    /// <summary>The merged catalog when <see cref="Succeeded"/> is true; otherwise null.</summary>
    public MergedCatalog? MergedCatalog { get; }

    /// <summary>
    /// The structural hash of <see cref="MergedCatalog"/> (<see cref="MergedCatalogCanonicalJson.ComputeStructuralHash"/>)
    /// when <see cref="Succeeded"/> is true; otherwise null.
    /// </summary>
    public string? MergedHash { get; }

    /// <summary>Every validation error detected when <see cref="Succeeded"/> is false; otherwise empty.</summary>
    public IReadOnlyList<SemanticOverlayValidationError> Errors => errors;

    public static CatalogRevision Success(DateTimeOffset createdAt, string technicalHash, MergedCatalog mergedCatalog, string mergedHash)
    {
        TechnicalCatalog.RequireIdentifier(technicalHash, nameof(technicalHash));
        ArgumentNullException.ThrowIfNull(mergedCatalog);
        TechnicalCatalog.RequireIdentifier(mergedHash, nameof(mergedHash));

        return new CatalogRevision(
            createdAt,
            CatalogRevisionStatus.Succeeded,
            technicalHash,
            mergedCatalog,
            mergedHash,
            Array.Empty<SemanticOverlayValidationError>());
    }

    public static CatalogRevision Failure(DateTimeOffset createdAt, string technicalHash, IEnumerable<SemanticOverlayValidationError> errors)
    {
        TechnicalCatalog.RequireIdentifier(technicalHash, nameof(technicalHash));
        ArgumentNullException.ThrowIfNull(errors);

        var copy = new ReadOnlyCollection<SemanticOverlayValidationError>(errors.ToArray());
        if (copy.Count == 0)
        {
            throw new ArgumentException("A failed revision requires at least one validation error.", nameof(errors));
        }

        if (copy.Any(static error => error is null))
        {
            throw new ArgumentException("Collections cannot contain null values.", nameof(errors));
        }

        return new CatalogRevision(createdAt, CatalogRevisionStatus.Failed, technicalHash, null, null, copy);
    }
}
