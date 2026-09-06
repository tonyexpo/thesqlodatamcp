using System.Text.Json;
using TheSqlODataMcp.Core.Catalog;

namespace TheSqlODataMcp.Persistence.CatalogStore;

/// <summary>
/// Converts between an in-memory <see cref="CatalogRevision"/> (Core) and its durable
/// <see cref="StoredCatalogRevision"/> row, in both directions. <see cref="ToStored"/> converts an
/// in-memory <see cref="CatalogRevision"/> into its durable row. <see cref="CatalogRevision"/> alone does
/// not retain the source <see cref="TechnicalCatalog"/> when the merge failed — only its hash — so the
/// technical catalog that produced a revision must be supplied separately: it is exactly the object a
/// caller already holds immediately before calling <see cref="CatalogRevisionFactory.Create"/>.
/// <see cref="ToDomain"/> is the inverse: it rebuilds a live <see cref="CatalogRevision"/> (and, for a
/// succeeded row, a live <see cref="MergedCatalog"/>) purely from a stored row's JSON, needed for the
/// handoff's lifecycle step 3, "load last active revision" — the case where no rebuild happens at startup
/// but the process still needs a usable catalog in memory (see ADR 0014's Consequences).
/// </summary>
public static class CatalogRevisionMapper
{
    public static StoredCatalogRevision ToStored(TechnicalCatalog technicalCatalog, CatalogRevision revision)
    {
        ArgumentNullException.ThrowIfNull(technicalCatalog);
        ArgumentNullException.ThrowIfNull(revision);

        var technicalCatalogJson = TechnicalCatalogCanonicalJson.Serialize(technicalCatalog);
        if (!string.Equals(TechnicalCatalogCanonicalJson.ComputeStructuralHash(technicalCatalog), revision.TechnicalHash, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The supplied technical catalog does not match the revision's technical hash.", nameof(technicalCatalog));
        }

        if (revision.Succeeded
            && !string.Equals(MergedCatalogCanonicalJson.ComputeStructuralHash(revision.MergedCatalog!), revision.MergedHash, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The revision's merged catalog does not match its own merged hash.", nameof(revision));
        }

        return revision.Succeeded
            ? StoredCatalogRevision.Success(
                revision.CreatedAt,
                revision.TechnicalHash,
                technicalCatalogJson,
                revision.MergedHash!,
                MergedCatalogCanonicalJson.Serialize(revision.MergedCatalog!))
            : StoredCatalogRevision.Failure(
                revision.CreatedAt,
                revision.TechnicalHash,
                technicalCatalogJson,
                SerializeErrors(revision.Errors));
    }

    /// <summary>
    /// Rebuilds a live <see cref="CatalogRevision"/> from a stored row: for a succeeded row, both the
    /// technical and merged catalog JSON are deserialized and the merged catalog's entities/fields are
    /// re-anchored to the freshly deserialized technical catalog's objects; for a failed row, only the
    /// validation errors are needed, matching how little <see cref="CatalogRevision.Failure"/> itself
    /// retains. Recomputes and verifies both structural hashes against the rebuilt objects before
    /// returning, mirroring <see cref="ToStored"/>'s own hash checks on the way in — a stored row is
    /// trusted control-store data today, but this is what would catch it if that ever stopped being true.
    /// </summary>
    public static CatalogRevision ToDomain(StoredCatalogRevision stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        if (!stored.Succeeded)
        {
            return CatalogRevision.Failure(stored.CreatedAt, stored.TechnicalHash, DeserializeErrors(stored.ValidationResultJson!));
        }

        var technicalCatalog = TechnicalCatalogCanonicalJson.Deserialize(stored.TechnicalCatalogJson);
        if (!string.Equals(TechnicalCatalogCanonicalJson.ComputeStructuralHash(technicalCatalog), stored.TechnicalHash, StringComparison.Ordinal))
        {
            throw new ArgumentException("The stored technical catalog JSON does not match its own technical hash.", nameof(stored));
        }

        var mergedCatalog = MergedCatalogCanonicalJson.Deserialize(stored.MergedCatalogJson!, technicalCatalog);
        if (!string.Equals(MergedCatalogCanonicalJson.ComputeStructuralHash(mergedCatalog), stored.MergedHash, StringComparison.Ordinal))
        {
            throw new ArgumentException("The stored merged catalog JSON does not match its own merged hash.", nameof(stored));
        }

        return CatalogRevision.Success(stored.CreatedAt, stored.TechnicalHash, mergedCatalog, stored.MergedHash!);
    }

    /// <summary>Deserializes <see cref="StoredCatalogRevision.ValidationResultJson"/> back into validation errors.</summary>
    public static IReadOnlyList<SemanticOverlayValidationError> DeserializeErrors(string validationResultJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(validationResultJson);
        var dtos = JsonSerializer.Deserialize<ValidationErrorDto[]>(validationResultJson)
            ?? throw new ArgumentException("The validation result JSON did not deserialize to an array.", nameof(validationResultJson));

        return dtos.Select(dto => new SemanticOverlayValidationError(dto.Code, dto.Path, dto.Message)).ToArray();
    }

    private static string SerializeErrors(IReadOnlyList<SemanticOverlayValidationError> errors) =>
        JsonSerializer.Serialize(errors.Select(error => new ValidationErrorDto(error.Code, error.Path, error.Message)));

    private sealed record ValidationErrorDto(string Code, string Path, string Message);
}
