using System.Collections.ObjectModel;

namespace TheSqlODataMcp.Core.Cqm;

/// <summary>
/// One independently detectable CQM problem, carrying a stable machine-readable code, a JSON-path-style
/// location, and a human-readable message. The shape intentionally mirrors the catalog validation errors
/// so every protocol adapter can expose one consistent error contract.
/// </summary>
public sealed class CqmValidationError
{
    public CqmValidationError(string code, string path, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(code));
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));
        ArgumentException.ThrowIfNullOrWhiteSpace(message, nameof(message));
        Code = code;
        Path = path;
        Message = message;
    }

    public string Code { get; }

    public string Path { get; }

    public string Message { get; }

    public override string ToString() => $"{Code} at {Path}: {Message}";
}

/// <summary>Stable machine-readable codes for every CQM parse and structural-validation problem.</summary>
public static class CqmValidationErrorCodes
{
    public const string JsonInvalid = "cqm.jsonInvalid";
    public const string LimitExceeded = "cqm.limitExceeded";
    public const string SchemaViolation = "cqm.schemaViolation";
    public const string DeserializationFailed = "cqm.deserializationFailed";
    public const string DuplicateAlias = "cqm.duplicateAlias";
    public const string DuplicateSelectAlias = "cqm.duplicateSelectAlias";
    public const string FieldReferenceInvalid = "cqm.fieldReferenceInvalid";
    public const string UnknownAlias = "cqm.unknownAlias";
    public const string AliasReferenceNotAllowed = "cqm.aliasReferenceNotAllowed";
    public const string AliasReferenceNotFound = "cqm.aliasReferenceNotFound";
    public const string OperatorArityInvalid = "cqm.operatorArityInvalid";
    public const string FunctionArityInvalid = "cqm.functionArityInvalid";
    public const string AggregateArityInvalid = "cqm.aggregateArityInvalid";
    public const string NestedAggregate = "cqm.nestedAggregate";
    public const string AggregateNotAllowedHere = "cqm.aggregateNotAllowedHere";
    public const string HavingWithoutAnalyticalQuery = "cqm.havingWithoutAnalyticalQuery";
    public const string JoinConditionInvalid = "cqm.joinConditionInvalid";
    public const string JoinConditionMissingSourceAlias = "cqm.joinConditionMissingSourceAlias";
    public const string DivisionByZero = "cqm.divisionByZero";
    public const string LiteralTypeInvalid = "cqm.literalTypeInvalid";
}

/// <summary>
/// The outcome of a strict CQM parse: either the validated <see cref="CqmQuery"/> or every independently
/// detectable structural error collected in one pass.
/// </summary>
public sealed class CqmParseResult
{
    /// <summary>
    /// SQL-A6a: the maximum number of errors returned to a caller. A pathological document can produce
    /// thousands of independently detectable schema violations; reporting all of them is neither useful
    /// nor bounded. <see cref="TotalErrors"/> still records how many were found.
    /// </summary>
    public const int MaxReportedErrors = 100;

    private readonly IReadOnlyList<CqmValidationError> errors;

    private CqmParseResult(CqmQuery? query, IReadOnlyList<CqmValidationError> errors, int totalErrors)
    {
        Query = query;
        this.errors = errors;
        TotalErrors = totalErrors;
    }

    public bool Succeeded => Query is not null;

    public CqmQuery? Query { get; }

    public IReadOnlyList<CqmValidationError> Errors => errors;

    /// <summary>Number of errors detected before the <see cref="MaxReportedErrors"/> cap.</summary>
    public int TotalErrors { get; }

    /// <summary>True when <see cref="TotalErrors"/> is greater than the reported <see cref="Errors"/>.</summary>
    public bool Truncated => TotalErrors > errors.Count;

    public static CqmParseResult Success(CqmQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return new CqmParseResult(query, Array.Empty<CqmValidationError>(), totalErrors: 0);
    }

    public static CqmParseResult Failure(IEnumerable<CqmValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var reported = new List<CqmValidationError>(MaxReportedErrors);
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

        return new CqmParseResult(null, new ReadOnlyCollection<CqmValidationError>(reported), totalErrors);
    }
}
