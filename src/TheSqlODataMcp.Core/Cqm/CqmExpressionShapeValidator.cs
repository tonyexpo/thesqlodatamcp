using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using TheSqlODataMcp.Core.Catalog;

namespace TheSqlODataMcp.Core.Cqm;

/// <summary>
/// Linear, allocation-bounded validation of the six CQM expression forms. The published JSON Schema keeps
/// the recursive <c>expression</c> definition as the client-facing contract, but the runtime evaluator uses
/// a permissive placeholder: JsonSchema.Net evaluates every branch of a recursive <c>oneOf</c> at every
/// level, which is exponential (a 636-byte depth-14 document needed 17 s and 3.2 GB — QA TS4-01/02).
/// This validator applies the same shape rules in one pass and returns the same stable paths.
/// </summary>
internal static class CqmExpressionShapeValidator
{
    private static readonly FrozenSet<string> ScalarTypes = FrozenSet.Create(
        StringComparer.Ordinal,
        "boolean", "int16", "int32", "int64", "decimal", "double", "string", "guid",
        "date", "time", "datetime", "datetimeOffset", "binary", "json");

    private static readonly FrozenSet<string> Operators = FrozenSet.Create(
        StringComparer.Ordinal,
        "and", "or", "not", "eq", "ne", "gt", "ge", "lt", "le", "in", "notIn", "between",
        "isNull", "isNotNull", "contains", "startsWith", "endsWith", "add", "subtract",
        "multiply", "divide", "modulo", "negate");

    private static readonly FrozenSet<string> Functions = FrozenSet.Create(
        StringComparer.Ordinal,
        "lower", "upper", "trim", "length", "concat", "substring", "year", "month", "day",
        "dateTrunc", "round", "floor", "ceiling", "absolute", "coalesce");

    private static readonly FrozenSet<string> Aggregates = FrozenSet.Create(
        StringComparer.Ordinal,
        "count", "sum", "average", "minimum", "maximum");

    public static IReadOnlyList<CqmValidationError> Validate(JsonElement root, CqmLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        var context = new Context(limits);
        if (root.ValueKind != JsonValueKind.Object)
        {
            return context.Errors;
        }

        if (root.TryGetProperty("where", out var where) && where.ValueKind != JsonValueKind.Null)
        {
            ValidateExpression(where, "$.where", depth: 0, context);
        }

        if (root.TryGetProperty("joins", out var joins) && joins.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var join in joins.EnumerateArray())
            {
                if (join.ValueKind == JsonValueKind.Object
                    && join.TryGetProperty("on", out var on)
                    && on.ValueKind == JsonValueKind.Object)
                {
                    ValidateExpression(on, Path("$.joins", index, "on"), depth: 0, context);
                }

                index += 1;
            }
        }

        if (root.TryGetProperty("select", out var select) && select.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in select.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("expression", out var expression)
                    && expression.ValueKind == JsonValueKind.Object)
                {
                    ValidateExpression(expression, Path("$.select", index, "expression"), depth: 0, context);
                }

                index += 1;
            }
        }

        if (root.TryGetProperty("groupBy", out var groupBy) && groupBy.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var expression in groupBy.EnumerateArray())
            {
                if (expression.ValueKind == JsonValueKind.Object)
                {
                    ValidateExpression(expression, Path("$.groupBy", index), depth: 0, context);
                }

                index += 1;
            }
        }

        if (root.TryGetProperty("having", out var having) && having.ValueKind != JsonValueKind.Null)
        {
            ValidateExpression(having, "$.having", depth: 0, context);
        }

        if (root.TryGetProperty("orderBy", out var orderBy) && orderBy.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in orderBy.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("expression", out var expression)
                    && expression.ValueKind == JsonValueKind.Object)
                {
                    ValidateExpression(expression, Path("$.orderBy", index, "expression"), depth: 0, context);
                }

                index += 1;
            }
        }

        return context.Errors;
    }

    private static void ValidateExpression(JsonElement element, string path, int depth, Context context)
    {
        if (context.LimitReached)
        {
            return;
        }

        if (depth > context.Limits.MaxExpressionDepth)
        {
            context.AddLimit(path, $"The CQM expression is nested deeper than {context.Limits.MaxExpressionDepth} levels.");
            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            context.Add(path, "The CQM expression must be a JSON object.");
            return;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            names.Add(property.Name);
        }

        var isField = names.SetEquals(["field"]);
        var isLiteral = names.IsSubsetOf(["value", "type"]) && names.Contains("value");
        var isAlias = names.SetEquals(["alias"]);
        var isOperator = names.SetEquals(["operator", "arguments"]);
        var isFunction = names.SetEquals(["function", "arguments"]);
        var isAggregate = names.IsSubsetOf(["aggregate", "expression", "distinct"]) && names.Contains("aggregate");

        var forms = 0;
        forms += isField ? 1 : 0;
        forms += isLiteral ? 1 : 0;
        forms += isAlias ? 1 : 0;
        forms += isOperator ? 1 : 0;
        forms += isFunction ? 1 : 0;
        forms += isAggregate ? 1 : 0;

        if (forms != 1)
        {
            context.Add(path, "The CQM expression must match exactly one of field, literal, alias, operator, function or aggregate.");
            return;
        }

        if (isField)
        {
            ValidateField(element, path, context);
            return;
        }

        if (isLiteral)
        {
            ValidateLiteral(element, path, context);
            return;
        }

        if (isAlias)
        {
            ValidateAlias(element, path, context);
            return;
        }

        if (isOperator)
        {
            ValidateOperator(element, path, depth, context);
            return;
        }

        if (isFunction)
        {
            ValidateFunction(element, path, depth, context);
            return;
        }

        ValidateAggregate(element, path, depth, context);
    }

    private static void ValidateField(JsonElement element, string path, Context context)
    {
        if (!element.TryGetProperty("field", out var field) || field.ValueKind != JsonValueKind.String)
        {
            context.Add(string.Concat(path, ".field"), "The field expression requires a string field reference.");
            return;
        }

        var value = field.GetString()!;
        var separator = value.IndexOf('.', StringComparison.Ordinal);
        if (value.Length is < 3 or > 512
            || separator <= 0
            || separator == value.Length - 1
            || !IsIdentifier(value[..separator])
            || value.AsSpan(separator + 1).ContainsAny('\n', '\r'))
        {
            context.Add(string.Concat(path, ".field"), "The field reference must use the alias.field form with a safe alias.");
        }
    }

    private static void ValidateAlias(JsonElement element, string path, Context context)
    {
        if (!element.TryGetProperty("alias", out var alias) || alias.ValueKind != JsonValueKind.String)
        {
            context.Add(string.Concat(path, ".alias"), "The alias expression requires a string alias.");
            return;
        }

        var value = alias.GetString()!;
        if (value.Length is < 1 or > 128 || !IsIdentifier(value))
        {
            context.Add(string.Concat(path, ".alias"), "The alias must match the CQM alias pattern.");
        }
    }

    private static void ValidateLiteral(JsonElement element, string path, Context context)
    {
        if (!element.TryGetProperty("value", out var value))
        {
            context.Add(string.Concat(path, ".value"), "The literal expression requires a value.");
            return;
        }

        if (!element.TryGetProperty("type", out var type))
        {
            return;
        }

        if (type.ValueKind != JsonValueKind.String || !ScalarTypes.Contains(type.GetString()!))
        {
            context.Add(string.Concat(path, ".type"), "The literal type is not part of the canonical scalar vocabulary.");
            return;
        }

        var typeName = type.GetString()!;
        if (!Enum.TryParse<CanonicalScalarType>(typeName, ignoreCase: true, out var scalar)
            || scalar == CanonicalScalarType.Unknown)
        {
            context.Add(string.Concat(path, ".type"), "The literal type is not part of the canonical scalar vocabulary.");
            return;
        }

        if (!HasJsonKind(value, scalar))
        {
            context.Add(string.Concat(path, ".value"), $"The literal value is not compatible with the declared canonical type '{typeName}'.");
            return;
        }

        // The published schema's range checks apply only when JsonSchema.Net sees a decimal-representable
        // number. CqmJson sanitizes numbers outside decimal range to zero before evaluation, so an
        // out-of-range double/int goes on to typed construction and fails there. Reproduce that split so
        // the established error codes do not change.
        if (scalar is CanonicalScalarType.Int16 or CanonicalScalarType.Int32 or CanonicalScalarType.Int64
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetDecimal(out _)
            && !CqmLiteralExpression.IsCompatible(value, scalar))
        {
            context.Add(string.Concat(path, ".value"), $"The literal value is not compatible with the declared canonical type '{typeName}'.");
        }
    }

    private static bool HasJsonKind(JsonElement value, CanonicalScalarType scalar) => scalar switch
    {
        CanonicalScalarType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        CanonicalScalarType.Int16 or CanonicalScalarType.Int32 or CanonicalScalarType.Int64
            or CanonicalScalarType.Decimal or CanonicalScalarType.Double => value.ValueKind == JsonValueKind.Number,
        CanonicalScalarType.String or CanonicalScalarType.Guid or CanonicalScalarType.Date
            or CanonicalScalarType.Time or CanonicalScalarType.DateTime or CanonicalScalarType.DateTimeOffset
            or CanonicalScalarType.Binary => value.ValueKind == JsonValueKind.String,
        CanonicalScalarType.Json => true,
        _ => false,
    };

    private static void ValidateOperator(JsonElement element, string path, int depth, Context context)
    {
        if (!element.TryGetProperty("operator", out var @operator)
            || @operator.ValueKind != JsonValueKind.String
            || !Operators.Contains(@operator.GetString()!))
        {
            context.Add(string.Concat(path, ".operator"), "The operator is not part of the CQM v1 vocabulary.");
        }

        ValidateArguments(element, path, depth, context);
    }

    private static void ValidateFunction(JsonElement element, string path, int depth, Context context)
    {
        if (!element.TryGetProperty("function", out var function)
            || function.ValueKind != JsonValueKind.String
            || !Functions.Contains(function.GetString()!))
        {
            context.Add(string.Concat(path, ".function"), "The function is not part of the CQM v1 vocabulary.");
        }

        ValidateArguments(element, path, depth, context);
    }

    private static void ValidateAggregate(JsonElement element, string path, int depth, Context context)
    {
        var aggregateName = string.Empty;
        if (!element.TryGetProperty("aggregate", out var aggregate)
            || aggregate.ValueKind != JsonValueKind.String
            || !Aggregates.Contains(aggregate.GetString()!))
        {
            context.Add(string.Concat(path, ".aggregate"), "The aggregate is not part of the CQM v1 vocabulary.");
        }
        else
        {
            aggregateName = aggregate.GetString()!;
        }

        if (aggregateName.Length == 0)
        {
            return;
        }

        var hasDistinct = element.TryGetProperty("distinct", out var distinct);
        if (hasDistinct && distinct.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            context.Add(string.Concat(path, ".distinct"), "The distinct flag must be a boolean.");
        }

        if (!element.TryGetProperty("expression", out var expression))
        {
            var requiresExpression = !string.Equals(aggregateName, "count", StringComparison.Ordinal)
                || (hasDistinct && distinct.ValueKind == JsonValueKind.True);
            if (requiresExpression)
            {
                context.Add(path, "The aggregate requires an expression.");
            }

            return;
        }

        if (expression.ValueKind != JsonValueKind.Object)
        {
            context.Add(string.Concat(path, ".expression"), "The aggregate expression must be a JSON object.");
            return;
        }

        ValidateExpression(expression, string.Concat(path, ".expression"), depth + 1, context);
    }

    private static void ValidateArguments(JsonElement element, string path, int depth, Context context)
    {
        if (!element.TryGetProperty("arguments", out var arguments) || arguments.ValueKind != JsonValueKind.Array)
        {
            context.Add(string.Concat(path, ".arguments"), "The expression requires an arguments array.");
            return;
        }

        if (arguments.GetArrayLength() == 0)
        {
            context.Add(string.Concat(path, ".arguments"), "The arguments array must contain at least one expression.");
            return;
        }

        var index = 0;
        foreach (var argument in arguments.EnumerateArray())
        {
            ValidateExpression(argument, Path(string.Concat(path, ".arguments"), index), depth + 1, context);
            if (context.LimitReached)
            {
                return;
            }

            index += 1;
        }
    }

    private static bool IsIdentifier(string value)
    {
        if (value.Length == 0 || !(char.IsAsciiLetter(value[0]) || value[0] == '_'))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (!(char.IsAsciiLetterOrDigit(character) || character == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static string Path(string prefix, int index, string? suffix = null)
    {
        var indexText = index.ToString(CultureInfo.InvariantCulture);
        return suffix is null ? $"{prefix}[{indexText}]" : $"{prefix}[{indexText}].{suffix}";
    }

    private sealed class Context(CqmLimits limits)
    {
        public CqmLimits Limits { get; } = limits;

        public List<CqmValidationError> Errors { get; } = [];

        public bool LimitReached { get; private set; }

        public void Add(string path, string message)
            => Errors.Add(new CqmValidationError(CqmValidationErrorCodes.SchemaViolation, path, message));

        public void AddLimit(string path, string message)
        {
            LimitReached = true;
            Errors.Add(new CqmValidationError(CqmValidationErrorCodes.LimitExceeded, path, message));
        }
    }
}
