using System.Collections.ObjectModel;

namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// A single, independently detectable problem found while importing a semantic overlay. Carries a
/// stable machine-readable code, a best-effort JSON-Schema-style pointer to the offending location, and
/// a human-readable message. Mirrors the general validation-error shape used elsewhere in the product
/// (code/path/message) rather than a bespoke ad hoc shape.
/// </summary>
public sealed class SemanticOverlayValidationError
{
    public SemanticOverlayValidationError(string code, string path, string message)
    {
        Code = TechnicalCatalog.RequireIdentifier(code, nameof(code));
        Path = TechnicalCatalog.RequireIdentifier(path, nameof(path));
        Message = TechnicalCatalog.RequireIdentifier(message, nameof(message));
    }

    /// <summary>A stable machine-readable error code from <see cref="SemanticOverlayValidationErrorCodes"/>.</summary>
    public string Code { get; }

    /// <summary>A JSON-pointer-style path (rooted at <c>$</c>) to the offending location in the overlay document.</summary>
    public string Path { get; }

    /// <summary>A human-readable description of the problem.</summary>
    public string Message { get; }

    public override string ToString() => $"{Code} at {Path}: {Message}";
}

/// <summary>
/// Stable machine-readable codes for every problem <see cref="SemanticOverlayImporter"/> can detect.
/// </summary>
public static class SemanticOverlayValidationErrorCodes
{
    /// <summary>A combined Markdown-with-front-matter import found no YAML front matter block.</summary>
    public const string MissingFrontMatter = "overlay.frontMatterMissing";

    /// <summary>The YAML text itself could not be parsed (malformed YAML syntax or an exceeded nesting/node bound).</summary>
    public const string YamlSyntaxInvalid = "overlay.yamlSyntaxInvalid";

    /// <summary>The Markdown body could not be parsed (for example, Markdig rejected an excessively nested document).</summary>
    public const string MarkdownInvalid = "overlay.markdownInvalid";

    /// <summary>
    /// Strict typed YAML deserialization rejected the document. Most commonly an unrecognized key
    /// (including any of the six forbidden top-level sections), but also covers scalar type-shape
    /// mismatches raised by the same strict-deserialization stage.
    /// </summary>
    public const string StrictDeserializationFailed = "overlay.strictDeserializationFailed";

    /// <summary>
    /// The versioned JSON Schema reported a structural or cross-field violation: a missing required
    /// field, an invalid enum value, a wrong scalar type, or an additional/forbidden property.
    /// </summary>
    public const string SchemaViolation = "overlay.schemaViolation";

    /// <summary><c>catalogVersion</c> is missing, null, or whitespace-only.</summary>
    public const string CatalogVersionRequired = "overlay.catalogVersionRequired";

    /// <summary>An entity <c>source</c> does not resolve to a physical entity in the supplied catalog.</summary>
    public const string EntitySourceNotFound = "overlay.entitySourceNotFound";

    /// <summary>Two overlay entity entries reference the same physical <c>source</c>.</summary>
    public const string EntitySourceDuplicate = "overlay.entitySourceDuplicate";

    /// <summary>A <c>fields</c> map key does not resolve to an existing field on the physical entity.</summary>
    public const string FieldNotFound = "overlay.fieldNotFound";

    /// <summary>A relationship <c>target</c> does not resolve to a physical entity in the supplied catalog.</summary>
    public const string RelationshipTargetNotFound = "overlay.relationshipTargetNotFound";

    /// <summary>A join pair's <c>sourceField</c> does not resolve to a field on the relationship's source entity.</summary>
    public const string JoinSourceFieldNotFound = "overlay.joinSourceFieldNotFound";

    /// <summary>A join pair's <c>targetField</c> does not resolve to a field on the relationship's target entity.</summary>
    public const string JoinTargetFieldNotFound = "overlay.joinTargetFieldNotFound";

    /// <summary>The YAML metadata is larger than the importer accepts.</summary>
    public const string YamlTooLarge = "overlay.yamlTooLarge";

    /// <summary>The Markdown narrative is larger than the importer accepts.</summary>
    public const string MarkdownTooLarge = "overlay.markdownTooLarge";

    /// <summary>A numeric scalar cannot be represented by the validation pipeline.</summary>
    public const string NumericOutOfRange = "overlay.numericOutOfRange";

    /// <summary>The YAML document is nested deeper or contains more nodes than the importer accepts.</summary>
    public const string YamlTooComplex = "overlay.yamlTooComplex";
}

/// <summary>
/// The outcome of a <see cref="SemanticOverlayImporter"/> import call: either the validated
/// <see cref="SemanticOverlay"/>, or every independently detectable validation error collected in one
/// pass. Administrators author overlay YAML by hand and need to see everything wrong at once rather
/// than fix-and-resubmit one error at a time, so this is a result type rather than throw-per-violation.
/// </summary>
public sealed class SemanticOverlayImportResult
{
    /// <summary>
    /// SQL-A6a: the maximum number of errors returned to an administrator. A wide invalid overlay can
    /// produce thousands of schema violations; the first errors are reported and
    /// <see cref="TotalErrors"/> records the full count.
    /// </summary>
    public const int MaxReportedErrors = 100;

    private readonly IReadOnlyList<SemanticOverlayValidationError> errors;

    private SemanticOverlayImportResult(
        SemanticOverlay? overlay,
        IReadOnlyList<SemanticOverlayValidationError> errors,
        int totalErrors)
    {
        Overlay = overlay;
        this.errors = errors;
        TotalErrors = totalErrors;
    }

    /// <summary>True when the import produced a valid <see cref="SemanticOverlay"/> with no errors.</summary>
    public bool Succeeded => Overlay is not null;

    /// <summary>The validated overlay when <see cref="Succeeded"/> is true; otherwise null.</summary>
    public SemanticOverlay? Overlay { get; }

    /// <summary>Validation errors detected, in detection order, capped at <see cref="MaxReportedErrors"/>.</summary>
    public IReadOnlyList<SemanticOverlayValidationError> Errors => errors;

    /// <summary>Number of errors detected before the <see cref="MaxReportedErrors"/> cap.</summary>
    public int TotalErrors { get; }

    /// <summary>True when <see cref="TotalErrors"/> is greater than the reported <see cref="Errors"/>.</summary>
    public bool Truncated => TotalErrors > errors.Count;

    public static SemanticOverlayImportResult Success(SemanticOverlay overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        return new SemanticOverlayImportResult(overlay, Array.Empty<SemanticOverlayValidationError>(), totalErrors: 0);
    }

    public static SemanticOverlayImportResult Failure(IEnumerable<SemanticOverlayValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var reported = new List<SemanticOverlayValidationError>(MaxReportedErrors);
        var totalErrors = 0;
        foreach (var error in errors)
        {
            totalErrors += 1;
            if (reported.Count < MaxReportedErrors)
            {
                reported.Add(error);
            }
        }

        if (totalErrors == 0)
        {
            throw new ArgumentException("A failure result requires at least one validation error.", nameof(errors));
        }

        return new SemanticOverlayImportResult(
            null,
            new ReadOnlyCollection<SemanticOverlayValidationError>(reported),
            totalErrors);
    }
}
