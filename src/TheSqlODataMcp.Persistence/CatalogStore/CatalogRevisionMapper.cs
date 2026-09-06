using System.Text.Json;
using TheSqlODataMcp.Core.Catalog;

namespace TheSqlODataMcp.Persistence.CatalogStore;

/// <summary>
/// Converts an in-memory <see cref="CatalogRevision"/> (Core) into its durable <see cref="StoredCatalogRevision"/>
/// row. <see cref="CatalogRevision"/> alone does not retain the source <see cref="TechnicalCatalog"/> when the
/// merge failed — only its hash — so the technical catalog that produced a revision must be supplied
/// separately: it is exactly the object a caller already holds immediately before calling
/// <see cref="CatalogRevisionFactory.Create"/>.
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
