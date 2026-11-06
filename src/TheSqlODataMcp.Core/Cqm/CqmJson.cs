using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Json.Schema;

namespace TheSqlODataMcp.Core.Cqm;

/// <summary>
/// Strict JSON entry point for the CQM v1 contract. Parsing runs the versioned JSON Schema first, then
/// typed construction, then the catalog-independent structural validator. Serialization emits the same
/// camel-case contract so a normalized query can be returned to a caller or persisted in diagnostics.
/// </summary>
public static class CqmJson
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    /// <summary>The exact published schema, available to hosts that expose it to clients.</summary>
    public static string SchemaJson => CqmSchema.Json;

    /// <summary>The schema/contract version.</summary>
    public static string SchemaVersion => CqmSchema.Version;

    public static string Serialize(CqmQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return JsonSerializer.Serialize(query, SerializerOptions);
    }

    public static CqmParseResult Deserialize(string json) => Deserialize(json, CqmLimits.Default);

    public static CqmParseResult Deserialize(string json, CqmLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        if (string.IsNullOrWhiteSpace(json))
        {
            return Failure(
                new CqmValidationError(
                    CqmValidationErrorCodes.JsonInvalid,
                    "$",
                    "The CQM JSON is empty."));
        }

        // SQL-A1: reject bytes, depth, token count and array widths before JsonDocument/JsonNode or
        // the schema evaluator can allocate the pathological document. UTF-8 is never shorter than the
        // UTF-16 length, so this first check avoids allocating the byte copy for an oversized string.
        if (json.Length > limits.MaxBytes)
        {
            return Failure(new CqmValidationError(
                CqmValidationErrorCodes.LimitExceeded,
                "$",
                $"The CQM document is {json.Length:N0} characters, above the {limits.MaxBytes:N0}-byte limit."));
        }

        var limitError = CqmRequestGuard.Validate(Encoding.UTF8.GetBytes(json), limits);
        if (limitError is not null)
        {
            return Failure(limitError);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (CqmUnicode.TryFindInvalidUnicode(document.RootElement, "$", out var invalidUnicodePath))
            {
                return Failure(
                    new CqmValidationError(
                        CqmValidationErrorCodes.JsonInvalid,
                        invalidUnicodePath,
                        "The CQM JSON contains an unpaired UTF-16 surrogate."));
            }

            if (TryFindDuplicateProperty(document.RootElement, "$", out var duplicatePath))
            {
                return Failure(
                    new CqmValidationError(
                        CqmValidationErrorCodes.JsonInvalid,
                        duplicatePath,
                        "Duplicate JSON property names are not allowed."));
            }

            // SQL-A2: the six expression forms are checked linearly here; the runtime schema treats
            // `expression` as an object so the recursive oneOf never reaches JsonSchema.Net.
            var shapeErrors = CqmExpressionShapeValidator.Validate(document.RootElement, limits);
            if (shapeErrors.Count > 0)
            {
                return CqmParseResult.Failure(shapeErrors);
            }
        }
        catch (JsonException ex)
        {
            return Failure(
                new CqmValidationError(
                    CqmValidationErrorCodes.JsonInvalid,
                    "$",
                    $"The CQM JSON is malformed: {ex.Message}"));
        }
        catch (ArgumentException)
        {
            return Failure(
                new CqmValidationError(
                    CqmValidationErrorCodes.JsonInvalid,
                    "$",
                    "The CQM JSON contains invalid UTF-16 text."));
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            return Failure(
                new CqmValidationError(
                    CqmValidationErrorCodes.JsonInvalid,
                    "$",
                    $"The CQM JSON is malformed: {ex.Message}"));
        }
        catch (ArgumentException)
        {
            return Failure(
                new CqmValidationError(
                    CqmValidationErrorCodes.JsonInvalid,
                    "$",
                    "The CQM JSON contains invalid UTF-16 text."));
        }

        if (node is null)
        {
            return Failure(
                new CqmValidationError(
                    CqmValidationErrorCodes.SchemaViolation,
                    "$",
                    "The CQM document must be a JSON object."));
        }

        // JsonSchema.Net converts numeric keywords such as minimum/maximum to decimal. A valid CQM
        // double/json literal can exceed decimal range (for example 1e29), which would make the schema
        // evaluator throw even though the document is valid. Evaluate a sanitized copy so structural
        // validation still runs; typed construction still sees the original JSON and enforces the
        // declared canonical type.
        SanitizeOutOfDecimalRangeNumbers(node);

        EvaluationResults evaluation;
        try
        {
            evaluation = CqmSchema.GetCompiled().Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.List });
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or JsonException or OverflowException or NotSupportedException)
        {
            return Failure(
                new CqmValidationError(
                    CqmValidationErrorCodes.SchemaViolation,
                    "$",
                    $"The CQM document could not be evaluated by the published schema: {ex.Message}"));
        }

        if (!evaluation.IsValid)
        {
            var errors = ConvertSchemaErrors(evaluation).ToList();
            if (errors.Count == 0)
            {
                errors.Add(new CqmValidationError(
                    CqmValidationErrorCodes.SchemaViolation,
                    "$",
                    "The CQM document does not satisfy the published v1 schema."));
            }

            return CqmParseResult.Failure(errors);
        }

        CqmQuery? query;
        try
        {
            query = JsonSerializer.Deserialize<CqmQuery>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            return Failure(
                new CqmValidationError(
                    CqmValidationErrorCodes.DeserializationFailed,
                    NormalizeExceptionPath(ex.Path),
                    $"Strict CQM deserialization failed: {ex.Message}"));
        }
        catch (ArgumentException ex)
        {
            return Failure(
                new CqmValidationError(
                    CqmValidationErrorCodes.DeserializationFailed,
                    "$",
                    $"CQM construction failed: {ex.Message}"));
        }
        catch (NotSupportedException ex)
        {
            return Failure(
                new CqmValidationError(
                    CqmValidationErrorCodes.DeserializationFailed,
                    "$",
                    $"CQM construction is not supported: {ex.Message}"));
        }

        if (query is null)
        {
            return Failure(
                new CqmValidationError(
                    CqmValidationErrorCodes.DeserializationFailed,
                    "$",
                    "The CQM JSON did not deserialize to a query."));
        }

        var validationErrors = CqmValidator.Validate(query);
        return validationErrors.Count == 0
            ? CqmParseResult.Success(query)
            : CqmParseResult.Failure(validationErrors);
    }

    private static CqmParseResult Failure(CqmValidationError error) => CqmParseResult.Failure([error]);

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

        options.Converters.Add(new CanonicalScalarTypeJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.Converters.Add(new CqmExpressionJsonConverter());
        return options;
    }

    private static IEnumerable<CqmValidationError> ConvertSchemaErrors(EvaluationResults evaluation)
    {
        var candidates = new List<SchemaErrorCandidate>();
        foreach (var detail in evaluation.Details)
        {
            if (!detail.HasErrors || HasValidAncestor(detail) || IsBranchNoise(detail))
            {
                continue;
            }

            var pointer = detail.InstanceLocation.ToString();
            var path = ToJsonPath(pointer);
            foreach (var error in detail.Errors!)
            {
                var message = string.IsNullOrEmpty(error.Key) ? error.Value : $"{error.Key}: {error.Value}";
                candidates.Add(new SchemaErrorCandidate(pointer, path, error.Key, message));
            }
        }

        // A combinator error is useful only when it is the deepest actionable location. When a nested
        // branch already pinpointed the offending value, the enclosing oneOf/anyOf error is redundant.
        var actionable = candidates
            .Where(candidate => !(candidate.Key is "oneOf" or "anyOf"
                && candidates.Any(other => IsStrictAncestor(candidate.Pointer, other.Pointer))))
            .ToList();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in actionable)
        {
            if (seen.Add(string.Concat(candidate.Path, "\u001F", candidate.Message)))
            {
                yield return new CqmValidationError(CqmValidationErrorCodes.SchemaViolation, candidate.Path, candidate.Message);
            }
        }
    }

    private static bool HasValidAncestor(EvaluationResults detail)
    {
        for (var parent = detail.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent.IsValid)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBranchNoise(EvaluationResults detail)
    {
        var evaluationPath = detail.EvaluationPath.ToString();
        if (evaluationPath.Contains("/if", StringComparison.Ordinal))
        {
            return true;
        }

        var inBranch = evaluationPath.Contains("/anyOf/", StringComparison.Ordinal)
            || evaluationPath.Contains("/oneOf/", StringComparison.Ordinal);
        if (!inBranch)
        {
            return false;
        }

        if (detail.Errors is { Count: 1 } errors
            && errors.TryGetValue(string.Empty, out var message)
            && message.StartsWith("All values fail against the false schema", StringComparison.Ordinal))
        {
            return true;
        }

        return detail.Errors!.Any(error => error.Key == "required"
            || (error.Key == "type" && error.Value.Contains("should be \"null\"", StringComparison.Ordinal)));
    }

    private static bool IsStrictAncestor(string ancestorPointer, string descendantPointer)
    {
        if (ancestorPointer.Length == 0)
        {
            return descendantPointer.Length > 0;
        }

        if (descendantPointer.Length <= ancestorPointer.Length
            || !descendantPointer.StartsWith(ancestorPointer, StringComparison.Ordinal))
        {
            return false;
        }

        return descendantPointer[ancestorPointer.Length] == '/';
    }

    private sealed record SchemaErrorCandidate(string Pointer, string Path, string Key, string Message);

    private static void SanitizeOutOfDecimalRangeNumbers(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    var value = property.Value;
                    if (value is null)
                    {
                        continue;
                    }

                    if (IsOutOfDecimalRangeNumber(value))
                    {
                        obj[property.Key] = JsonValue.Create(0m);
                    }
                    else
                    {
                        SanitizeOutOfDecimalRangeNumbers(value);
                    }
                }

                break;
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    var value = array[index];
                    if (value is null)
                    {
                        continue;
                    }

                    if (IsOutOfDecimalRangeNumber(value))
                    {
                        array[index] = JsonValue.Create(0m);
                    }
                    else
                    {
                        SanitizeOutOfDecimalRangeNumbers(value);
                    }
                }

                break;
        }
    }

    private static bool IsOutOfDecimalRangeNumber(JsonNode node) =>
        node is JsonValue value
        && value.GetValueKind() == JsonValueKind.Number
        && !value.TryGetValue<decimal>(out _);

    private static bool TryFindDuplicateProperty(JsonElement element, string path, out string duplicatePath)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = string.Concat(path, ".", property.Name);
                if (!names.Add(property.Name))
                {
                    duplicatePath = propertyPath;
                    return true;
                }

                if (TryFindDuplicateProperty(property.Value, propertyPath, out duplicatePath))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                var itemPath = string.Create(CultureInfo.InvariantCulture, $"{path}[{index}]");
                if (TryFindDuplicateProperty(item, itemPath, out duplicatePath))
                {
                    return true;
                }

                index++;
            }
        }

        duplicatePath = string.Empty;
        return false;
    }

    private static string NormalizeExceptionPath(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? "$"
            : path.StartsWith('$')
                ? path
                : string.Concat("$", path);

    private static string ToJsonPath(string pointer)
    {
        if (pointer.Length == 0)
        {
            return "$";
        }

        var builder = new StringBuilder("$");
        foreach (var segment in pointer.Split('/').Skip(1))
        {
            var unescaped = segment
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);

            if (int.TryParse(unescaped, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                builder.Append('[').Append(unescaped).Append(']');
            }
            else
            {
                builder.Append('.').Append(unescaped);
            }
        }

        return builder.ToString();
    }
}
