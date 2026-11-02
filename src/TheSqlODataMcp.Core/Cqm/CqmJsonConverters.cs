using System.Text.Json;
using System.Text.Json.Serialization;
using TheSqlODataMcp.Core.Catalog;

namespace TheSqlODataMcp.Core.Cqm;

internal sealed class CanonicalScalarTypeJsonConverter : JsonConverter<CanonicalScalarType>
{
    public override CanonicalScalarType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var wireName = reader.GetString()
            ?? throw new JsonException("A canonical scalar type must be a string.");

        return FromWireName(wireName);
    }

    public override void Write(Utf8JsonWriter writer, CanonicalScalarType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ToWireName(value));
    }

    internal static CanonicalScalarType FromWireName(string wireName) => wireName switch
    {
        "boolean" => CanonicalScalarType.Boolean,
        "int16" => CanonicalScalarType.Int16,
        "int32" => CanonicalScalarType.Int32,
        "int64" => CanonicalScalarType.Int64,
        "decimal" => CanonicalScalarType.Decimal,
        "double" => CanonicalScalarType.Double,
        "string" => CanonicalScalarType.String,
        "guid" => CanonicalScalarType.Guid,
        "date" => CanonicalScalarType.Date,
        "time" => CanonicalScalarType.Time,
        "datetime" => CanonicalScalarType.DateTime,
        "datetimeOffset" => CanonicalScalarType.DateTimeOffset,
        "binary" => CanonicalScalarType.Binary,
        "json" => CanonicalScalarType.Json,
        "unknown" => CanonicalScalarType.Unknown,
        _ => throw new JsonException($"Unknown canonical scalar type '{wireName}'."),
    };

    internal static string ToWireName(CanonicalScalarType value) => value switch
    {
        CanonicalScalarType.Boolean => "boolean",
        CanonicalScalarType.Int16 => "int16",
        CanonicalScalarType.Int32 => "int32",
        CanonicalScalarType.Int64 => "int64",
        CanonicalScalarType.Decimal => "decimal",
        CanonicalScalarType.Double => "double",
        CanonicalScalarType.String => "string",
        CanonicalScalarType.Guid => "guid",
        CanonicalScalarType.Date => "date",
        CanonicalScalarType.Time => "time",
        CanonicalScalarType.DateTime => "datetime",
        CanonicalScalarType.DateTimeOffset => "datetimeOffset",
        CanonicalScalarType.Binary => "binary",
        CanonicalScalarType.Json => "json",
        CanonicalScalarType.Unknown => "unknown",
        _ => throw new JsonException($"Unknown canonical scalar type '{value}'."),
    };
}

internal sealed class CqmExpressionJsonConverter : JsonConverter<CqmExpression>
{
    private static readonly string[] FormNames = ["field", "value", "alias", "operator", "function", "aggregate"];

    public override CqmExpression Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A CQM expression must be a JSON object.");
        }

        var forms = root
            .EnumerateObject()
            .Select(static property => property.Name)
            .Where(static name => FormNames.Contains(name, StringComparer.Ordinal))
            .ToArray();

        if (forms.Length != 1)
        {
            throw new JsonException("A CQM expression must contain exactly one of field, value, alias, operator, function, or aggregate.");
        }

        return forms[0] switch
        {
            "field" => ReadField(root),
            "value" => ReadLiteral(root),
            "alias" => ReadAlias(root),
            "operator" => ReadOperator(root, options),
            "function" => ReadFunction(root, options),
            "aggregate" => ReadAggregate(root, options),
            _ => throw new JsonException("Unsupported CQM expression form."),
        };
    }

    public override void Write(Utf8JsonWriter writer, CqmExpression value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case CqmFieldExpression field:
                writer.WriteString("field", field.Field);
                break;
            case CqmLiteralExpression literal:
                writer.WritePropertyName("value");
                literal.Value.WriteTo(writer);
                if (literal.Type is not null)
                {
                    writer.WriteString("type", CanonicalScalarTypeJsonConverter.ToWireName(literal.Type.Value));
                }

                break;
            case CqmAliasExpression alias:
                writer.WriteString("alias", alias.Alias);
                break;
            case CqmOperatorExpression @operator:
                writer.WriteString("operator", JsonNamingPolicy.CamelCase.ConvertName(@operator.Operator.ToString()));
                WriteArguments(writer, @operator.Arguments, options);
                break;
            case CqmFunctionExpression function:
                writer.WriteString("function", JsonNamingPolicy.CamelCase.ConvertName(function.Function.ToString()));
                WriteArguments(writer, function.Arguments, options);
                break;
            case CqmAggregateExpression aggregate:
                writer.WriteString("aggregate", JsonNamingPolicy.CamelCase.ConvertName(aggregate.Aggregate.ToString()));
                if (aggregate.Expression is not null)
                {
                    writer.WritePropertyName("expression");
                    JsonSerializer.Serialize(writer, aggregate.Expression, options);
                }

                writer.WriteBoolean("distinct", aggregate.Distinct);
                break;
            default:
                throw new JsonException($"Unsupported CQM expression type '{value.GetType().Name}'.");
        }

        writer.WriteEndObject();
    }

    private static CqmFieldExpression ReadField(JsonElement root)
    {
        EnsureOnlyProperties(root, "field");
        return new CqmFieldExpression(ReadRequiredString(root, "field"));
    }

    private static CqmLiteralExpression ReadLiteral(JsonElement root)
    {
        EnsureOnlyProperties(root, "value", "type");
        if (!root.TryGetProperty("value", out var value))
        {
            throw new JsonException("A literal expression requires value.");
        }

        CanonicalScalarType? type = null;
        if (root.TryGetProperty("type", out var typeElement))
        {
            if (typeElement.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("A literal type must be a string.");
            }

            type = CanonicalScalarTypeJsonConverter.FromWireName(typeElement.GetString()!);
        }

        return new CqmLiteralExpression(value.Clone(), type);
    }

    private static CqmAliasExpression ReadAlias(JsonElement root)
    {
        EnsureOnlyProperties(root, "alias");
        return new CqmAliasExpression(ReadRequiredString(root, "alias"));
    }

    private static CqmOperatorExpression ReadOperator(JsonElement root, JsonSerializerOptions options)
    {
        EnsureOnlyProperties(root, "operator", "arguments");
        var @operator = ReadRequiredEnum<CqmOperator>(root, "operator", options);
        return new CqmOperatorExpression(@operator, ReadArguments(root, options));
    }

    private static CqmFunctionExpression ReadFunction(JsonElement root, JsonSerializerOptions options)
    {
        EnsureOnlyProperties(root, "function", "arguments");
        var function = ReadRequiredEnum<CqmFunction>(root, "function", options);
        return new CqmFunctionExpression(function, ReadArguments(root, options));
    }

    private static CqmAggregateExpression ReadAggregate(JsonElement root, JsonSerializerOptions options)
    {
        EnsureOnlyProperties(root, "aggregate", "expression", "distinct");
        var aggregate = ReadRequiredEnum<CqmAggregate>(root, "aggregate", options);

        CqmExpression? expression = null;
        if (root.TryGetProperty("expression", out var expressionElement))
        {
            expression = expressionElement.Deserialize<CqmExpression>(options)
                ?? throw new JsonException("An aggregate expression cannot be null.");
        }

        var distinct = root.TryGetProperty("distinct", out var distinctElement) && distinctElement.GetBoolean();
        return new CqmAggregateExpression(aggregate, expression, distinct);
    }

    private static List<CqmExpression> ReadArguments(JsonElement root, JsonSerializerOptions options)
    {
        if (!root.TryGetProperty("arguments", out var arguments) || arguments.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("An operator or function requires an arguments array.");
        }

        var result = new List<CqmExpression>();
        foreach (var argument in arguments.EnumerateArray())
        {
            result.Add(argument.Deserialize<CqmExpression>(options)
                ?? throw new JsonException("An expression argument cannot be null."));
        }

        return result;
    }

    private static TEnum ReadRequiredEnum<TEnum>(JsonElement root, string propertyName, JsonSerializerOptions options)
        where TEnum : struct, Enum
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            throw new JsonException($"A CQM expression requires {propertyName}.");
        }

        return element.Deserialize<TEnum>(options);
    }

    private static string ReadRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"A CQM expression requires a string {propertyName}.");
        }

        return element.GetString()!;
    }

    private static void WriteArguments(Utf8JsonWriter writer, IReadOnlyList<CqmExpression> arguments, JsonSerializerOptions options)
    {
        writer.WriteStartArray("arguments");
        foreach (var argument in arguments)
        {
            JsonSerializer.Serialize(writer, argument, options);
        }

        writer.WriteEndArray();
    }

    private static void EnsureOnlyProperties(JsonElement root, params string[] allowed)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new JsonException($"Unexpected CQM expression property '{property.Name}'.");
            }
        }
    }
}
