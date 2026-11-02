using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheSqlODataMcp.Core.Catalog;

namespace TheSqlODataMcp.Core.Cqm;

/// <summary>
/// One CQM expression. Exactly one concrete form is represented by a subclass: field, literal, alias,
/// operator, function, or aggregate. The JSON representation is a strict discriminated union.
/// </summary>
public abstract class CqmExpression
{
    private protected CqmExpression()
    {
    }
}

/// <summary>A physical field reference in <c>alias.field</c> form.</summary>
public sealed class CqmFieldExpression : CqmExpression
{
    public CqmFieldExpression(string field)
    {
        Field = CqmNames.RequireFieldReference(field, nameof(field));
    }

    public string Field { get; }
}

/// <summary>
/// A literal value. <see cref="Type"/> is optional; when declared it must belong to the public canonical
/// scalar vocabulary and be compatible with the JSON value.
/// </summary>
public sealed class CqmLiteralExpression : CqmExpression
{
    public CqmLiteralExpression(JsonElement value, CanonicalScalarType? type = null)
    {
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("A literal value is required.", nameof(value));
        }

        if (CqmUnicode.TryFindInvalidUnicode(value, "$", out var invalidUnicodePath))
        {
            throw new ArgumentException(
                $"The literal contains an unpaired UTF-16 surrogate at {invalidUnicodePath}.",
                nameof(value));
        }

        if (type is not null)
        {
            if (!Enum.IsDefined(type.Value) || type.Value == CanonicalScalarType.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(type), type, "A literal type must be a supported concrete canonical scalar type.");
            }

            if (!IsCompatible(value, type.Value))
            {
                throw new ArgumentException($"The literal value is not compatible with the declared canonical type '{type.Value}'.", nameof(value));
            }
        }

        Value = value.Clone();
        Type = type;
    }

    public JsonElement Value { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CanonicalScalarType? Type { get; }

    internal static bool IsCompatible(JsonElement value, CanonicalScalarType type) => type switch
    {
        CanonicalScalarType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        CanonicalScalarType.Int16 => IsIntegerInRange(value, short.MinValue, short.MaxValue),
        CanonicalScalarType.Int32 => IsIntegerInRange(value, int.MinValue, int.MaxValue),
        CanonicalScalarType.Int64 => IsIntegerInRange(value, long.MinValue, long.MaxValue),
        CanonicalScalarType.Decimal => value.ValueKind == JsonValueKind.Number
            && value.TryGetDecimal(out var decimalValue)
            && (decimalValue != 0 || IsJsonZero(value)),
        CanonicalScalarType.Double => value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var doubleValue)
            && double.IsFinite(doubleValue)
            && (doubleValue != 0 || IsJsonZero(value)),
        CanonicalScalarType.String => value.ValueKind == JsonValueKind.String,
        CanonicalScalarType.Guid => value.ValueKind == JsonValueKind.String
            && Guid.TryParseExact(value.GetString()!, "D", out _),
        CanonicalScalarType.Date => value.ValueKind == JsonValueKind.String
            && DateOnly.TryParseExact(value.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        CanonicalScalarType.Time => value.ValueKind == JsonValueKind.String
            && IsCanonicalTime(value.GetString()!),
        CanonicalScalarType.DateTime => value.ValueKind == JsonValueKind.String
            && IsCanonicalDateTime(value.GetString()!),
        CanonicalScalarType.DateTimeOffset => value.ValueKind == JsonValueKind.String
            && IsCanonicalDateTimeOffset(value.GetString()!),
        CanonicalScalarType.Binary => value.ValueKind == JsonValueKind.String
            && IsBase64(value.GetString()!),
        CanonicalScalarType.Json => true,
        _ => false,
    };

    /// <summary>
    /// Canonical time-only grammar: a complete <c>HH:mm:ss</c> with up to seven fractional digits. No date, no
    /// timezone, no culture-dependent parsing (TS2-02).
    /// </summary>
    private static bool IsCanonicalTime(string value) =>
        TimeOnly.TryParseExact(
            value,
            ["HH:mm:ss", "HH:mm:ss.FFFFFFF"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);

    /// <summary>
    /// Canonical local datetime grammar: complete date and time with the <c>T</c> separator and up to seven fractional
    /// digits. An offset is deliberately not accepted here; use <c>datetimeOffset</c> when the instant is offset-bearing.
    /// </summary>
    private static bool IsCanonicalDateTime(string value) =>
        DateTime.TryParseExact(
            value,
            ["yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);

    /// <summary>
    /// Canonical offset datetime grammar: complete date and time, <c>T</c> separator, up to seven fractional digits,
    /// and a mandatory <c>Z</c> or <c>±HH:mm</c> offset.
    /// </summary>
    private static bool IsCanonicalDateTimeOffset(string value) =>
        DateTimeOffset.TryParseExact(
            value,
            ["yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _)
        || DateTimeOffset.TryParseExact(
            value,
            ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out _);

    internal static bool IsJsonZero(JsonElement value)
    {
        var text = value.GetRawText();
        if (text.Length > 0 && text[0] == '-')
        {
            text = text[1..];
        }

        var exponent = text.IndexOfAny('e', 'E');
        if (exponent >= 0)
        {
            text = text[..exponent];
        }

        return text.Length > 0 && text.All(static character => character is '0' or '.');
    }

    private static bool IsIntegerInRange(JsonElement value, long minimum, long maximum) =>
        value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
        && number >= minimum
        && number <= maximum
        && (number != 0 || IsJsonZero(value));

    private static bool IsBase64(string value)
    {
        if (value.Length == 0)
        {
            return true;
        }

        return Convert.TryFromBase64String(value, new byte[value.Length], out _);
    }
}

/// <summary>A reference to one alias selected earlier in the same query.</summary>
public sealed class CqmAliasExpression : CqmExpression
{
    public CqmAliasExpression(string alias)
    {
        Alias = CqmNames.RequireAlias(alias, nameof(alias));
    }

    public string Alias { get; }
}

public enum CqmOperator
{
    And,
    Or,
    Not,
    Eq,
    Ne,
    Gt,
    Ge,
    Lt,
    Le,
    In,
    NotIn,
    Between,
    IsNull,
    IsNotNull,
    Contains,
    StartsWith,
    EndsWith,
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    Negate,
}

/// <summary>One provider-neutral operator application.</summary>
public sealed class CqmOperatorExpression : CqmExpression
{
    private readonly IReadOnlyList<CqmExpression> arguments;

    public CqmOperatorExpression(CqmOperator @operator, IReadOnlyList<CqmExpression> arguments)
    {
        if (!Enum.IsDefined(@operator))
        {
            throw new ArgumentOutOfRangeException(nameof(@operator), @operator, "Unknown CQM operator.");
        }

        this.arguments = CqmCollections.Copy(arguments, nameof(arguments));
        if (this.arguments.Count == 0)
        {
            throw new ArgumentException("An operator requires at least one argument.", nameof(arguments));
        }

        Operator = @operator;
    }

    public CqmOperator Operator { get; }

    public IReadOnlyList<CqmExpression> Arguments => arguments;
}

public enum CqmFunction
{
    Lower,
    Upper,
    Trim,
    Length,
    Concat,
    Substring,
    Year,
    Month,
    Day,
    DateTrunc,
    Round,
    Floor,
    Ceiling,
    Absolute,
    Coalesce,
}

/// <summary>One provider-neutral scalar function application.</summary>
public sealed class CqmFunctionExpression : CqmExpression
{
    private readonly IReadOnlyList<CqmExpression> arguments;

    public CqmFunctionExpression(CqmFunction function, IReadOnlyList<CqmExpression> arguments)
    {
        if (!Enum.IsDefined(function))
        {
            throw new ArgumentOutOfRangeException(nameof(function), function, "Unknown CQM function.");
        }

        this.arguments = CqmCollections.Copy(arguments, nameof(arguments));
        if (this.arguments.Count == 0)
        {
            throw new ArgumentException("A function requires at least one argument.", nameof(arguments));
        }

        Function = function;
    }

    public CqmFunction Function { get; }

    public IReadOnlyList<CqmExpression> Arguments => arguments;
}

public enum CqmAggregate
{
    Count,
    Sum,
    Average,
    Minimum,
    Maximum,
}

/// <summary>
/// One aggregate. <c>count</c> may omit its expression for a row count; every other aggregate requires one.
/// </summary>
public sealed class CqmAggregateExpression : CqmExpression
{
    public CqmAggregateExpression(CqmAggregate aggregate, CqmExpression? expression = null, bool distinct = false)
    {
        if (!Enum.IsDefined(aggregate))
        {
            throw new ArgumentOutOfRangeException(nameof(aggregate), aggregate, "Unknown CQM aggregate.");
        }

        if (aggregate != CqmAggregate.Count && expression is null)
        {
            throw new ArgumentException("This aggregate requires an expression.", nameof(expression));
        }

        if (distinct && expression is null)
        {
            throw new ArgumentException("A distinct aggregate requires an expression.", nameof(expression));
        }

        Aggregate = aggregate;
        Expression = expression;
        Distinct = distinct;
    }

    public CqmAggregate Aggregate { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CqmExpression? Expression { get; }

    public bool Distinct { get; }
}
