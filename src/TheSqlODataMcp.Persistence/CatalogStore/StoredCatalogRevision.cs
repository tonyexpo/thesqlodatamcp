using TheSqlODataMcp.Core.Catalog;

namespace TheSqlODataMcp.Persistence.CatalogStore;

/// <summary>
/// The durable row for one <see cref="CatalogRevision"/> build attempt. <see cref="CatalogRevision"/>
/// itself deliberately carries no identity, "active" flag, or persistence concept (see ADR 0012); this
/// type adds exactly the storage-boundary concepts ADR 0012 deferred — an append-only, database-generated
/// <see cref="Id"/>, and (per ADR 0014) an <see cref="ActivatedAt"/> timestamp set only through
/// <see cref="Activate"/>, matching the handoff's own indicative <c>CatalogRevisions</c> schema
/// (<c>Id</c>, <c>CreatedAt</c>, <c>ActivatedAt</c>, <c>Status</c>, ...) rather than a separate pointer table.
/// </summary>
public sealed class StoredCatalogRevision
{
    /// <summary>EF Core materialization only — reads back through the backing fields, bypassing validation.</summary>
    private StoredCatalogRevision()
    {
        TechnicalHash = null!;
        TechnicalCatalogJson = null!;
    }

    public StoredCatalogRevision(
        DateTimeOffset createdAt,
        CatalogRevisionStatus status,
        string technicalHash,
        string technicalCatalogJson,
        string? mergedHash,
        string? mergedCatalogJson,
        string? validationResultJson)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "The catalog revision status is not supported.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(technicalHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(technicalCatalogJson);

        if (status == CatalogRevisionStatus.Succeeded)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(mergedHash, nameof(mergedHash));
            ArgumentException.ThrowIfNullOrWhiteSpace(mergedCatalogJson, nameof(mergedCatalogJson));
            if (validationResultJson is not null)
            {
                throw new ArgumentException("A succeeded revision cannot carry a validation result.", nameof(validationResultJson));
            }
        }
        else
        {
            if (mergedHash is not null)
            {
                throw new ArgumentException("A failed revision cannot carry a merged hash.", nameof(mergedHash));
            }

            if (mergedCatalogJson is not null)
            {
                throw new ArgumentException("A failed revision cannot carry a merged catalog.", nameof(mergedCatalogJson));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(validationResultJson, nameof(validationResultJson));
        }

        CreatedAt = createdAt;
        Status = status;
        TechnicalHash = technicalHash;
        TechnicalCatalogJson = technicalCatalogJson;
        MergedHash = mergedHash;
        MergedCatalogJson = mergedCatalogJson;
        ValidationResultJson = validationResultJson;
    }

    /// <summary>Database-generated surrogate key. Zero for a not-yet-saved instance.</summary>
    public long Id { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public CatalogRevisionStatus Status { get; private set; }

    /// <summary>True iff <see cref="Status"/> is <see cref="CatalogRevisionStatus.Succeeded"/>.</summary>
    public bool Succeeded => Status == CatalogRevisionStatus.Succeeded;

    public string TechnicalHash { get; private set; }

    /// <summary>
    /// <see cref="TechnicalCatalogCanonicalJson.Serialize"/> output for the source technical catalog.
    /// Always present: technical discovery happens before merging, so it exists even when the merge failed.
    /// </summary>
    public string TechnicalCatalogJson { get; private set; }

    /// <summary>Present iff <see cref="Succeeded"/>.</summary>
    public string? MergedHash { get; private set; }

    /// <summary><see cref="MergedCatalogCanonicalJson.Serialize"/> output. Present iff <see cref="Succeeded"/>.</summary>
    public string? MergedCatalogJson { get; private set; }

    /// <summary>Serialized <see cref="SemanticOverlayValidationError"/> list. Present iff not <see cref="Succeeded"/>.</summary>
    public string? ValidationResultJson { get; private set; }

    /// <summary>
    /// When this row most recently served as the active revision, or null if it never has. "Currently
    /// active" is defined as the row with the greatest non-null <see cref="ActivatedAt"/>
    /// (<see cref="CatalogRevisionStore.GetActiveAsync"/>) — activating a new revision therefore never
    /// requires clearing this field on any other row.
    /// </summary>
    public DateTimeOffset? ActivatedAt { get; private set; }

    /// <summary>
    /// Marks this row as the currently active revision as of <paramref name="activatedAt"/>. A failed
    /// revision can never be activated — this is the entire "last-valid rollback" guarantee: a caller that
    /// only ever calls <see cref="Activate"/> after checking <see cref="Succeeded"/> will simply never
    /// reach this throw, and the previously active row is left untouched.
    /// </summary>
    public void Activate(DateTimeOffset activatedAt)
    {
        if (!Succeeded)
        {
            throw new InvalidOperationException("A failed catalog revision cannot be activated.");
        }

        ActivatedAt = activatedAt;
    }

    public static StoredCatalogRevision Success(
        DateTimeOffset createdAt, string technicalHash, string technicalCatalogJson, string mergedHash, string mergedCatalogJson) =>
        new(createdAt, CatalogRevisionStatus.Succeeded, technicalHash, technicalCatalogJson, mergedHash, mergedCatalogJson, null);

    public static StoredCatalogRevision Failure(
        DateTimeOffset createdAt, string technicalHash, string technicalCatalogJson, string validationResultJson) =>
        new(createdAt, CatalogRevisionStatus.Failed, technicalHash, technicalCatalogJson, null, null, validationResultJson);
}
