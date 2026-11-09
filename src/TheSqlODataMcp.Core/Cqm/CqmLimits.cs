using System.Globalization;
using System.Text.Json;

namespace TheSqlODataMcp.Core.Cqm;

/// <summary>
/// Hard bounds applied to a CQM document before any schema evaluation or typed construction. The defaults
/// mirror the <c>Query:*</c> configuration where a key exists and add the request-boundary limits that the
/// published schema cannot enforce cheaply (bytes, JSON depth, token count, array widths).
/// </summary>
public sealed record CqmLimits
{
    /// <summary>The limits used by <see cref="CqmJson.Deserialize(string)"/> unless a host supplies its own.</summary>
    public static CqmLimits Default { get; } = new();

    /// <summary>Maximum UTF-8 payload accepted at the boundary. Default 1 MiB.</summary>
    public int MaxBytes { get; init; } = 1_048_576;

    /// <summary>Maximum JSON nesting (objects and arrays). Default 64.</summary>
    public int MaxJsonDepth { get; init; } = 64;

    /// <summary>Maximum JSON tokens read while guarding the document. Default 200,000.</summary>
    public int MaxTokens { get; init; } = 200_000;

    /// <summary>Maximum number of <c>select</c> items. Default 1,000.</summary>
    public int MaxSelectItems { get; init; } = 1_000;

    /// <summary>Maximum number of joins. Default 8, matching <c>Query:MaxJoins</c>.</summary>
    public int MaxJoins { get; init; } = 8;

    /// <summary>Maximum number of grouping expressions. Default 12, matching <c>Query:MaxGroupByFields</c>.</summary>
    public int MaxGroupByFields { get; init; } = 12;

    /// <summary>Maximum number of ordering keys. Default 12.</summary>
    public int MaxOrderByFields { get; init; } = 12;

    /// <summary>Maximum number of arguments in one operator or function call. Default 100.</summary>
    public int MaxArguments { get; init; } = 100;

    /// <summary>Maximum <c>page.limit</c> accepted at the boundary. Default 5,000, matching <c>Query:MaxPageSize</c>.</summary>
    public int MaxPageSize { get; init; } = 5_000;

    /// <summary>Maximum expression nesting validated by the typed shape validator. Default 20, matching <c>Query:MaxExpressionDepth</c>.</summary>
    public int MaxExpressionDepth { get; init; } = 20;
}

/// <summary>
/// Single-pass <see cref="Utf8JsonReader"/> guard that rejects a CQM document before it can allocate a
/// <see cref="System.Text.Json.Nodes.JsonNode"/> tree or enter the recursive JSON Schema evaluator.
/// </summary>
internal static class CqmRequestGuard
{
    // A high parser depth keeps Utf8JsonReader iterative; the limit that matters is checked on our own
    // stack so a malformed document is still reported as malformed, not as a limit violation.
    private const int ReaderDepth = 1_024;

    public static CqmValidationError? Validate(ReadOnlySpan<byte> utf8, CqmLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        if (utf8.Length > limits.MaxBytes)
        {
            return Limit(
                "$",
                $"The CQM document is {utf8.Length:N0} bytes, above the limit of {limits.MaxBytes:N0} bytes.");
        }

        // SQL7-02: the reader must be able to see documents up to the configured depth so our own
        // stack counter reports the limit violation (and not a generic malformed-JSON error). The
        // extra level lets the guard observe the first token beyond the configured bound.
        var readerDepth = (int)Math.Clamp((long)limits.MaxJsonDepth + 1, ReaderDepth, 1_000_000);
        var reader = new Utf8JsonReader(
            utf8,
            new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false,
                MaxDepth = readerDepth,
            });

        var stack = new Stack<Frame>();
        var tokens = 0;
        string? pendingProperty = null;

        try
        {
            while (reader.Read())
            {
                tokens += 1;
                if (tokens > limits.MaxTokens)
                {
                    return Limit(CurrentPath(stack, pendingProperty), $"The CQM document contains more than {limits.MaxTokens:N0} JSON tokens.");
                }

                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        pendingProperty = reader.GetString();
                        break;

                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        {
                            var path = ValuePath(stack, pendingProperty);
                            if (stack.Count + 1 > limits.MaxJsonDepth)
                            {
                                return Limit(path, $"The CQM document is nested deeper than {limits.MaxJsonDepth} JSON levels.");
                            }

                            if (stack.Count > 0 && stack.Peek().IsArray)
                            {
                                var parent = stack.Peek();
                                parent.Count += 1;
                                var limit = parent.InLiteralValue ? null : ArrayLimit(parent.Name, limits);
                                if (limit is not null && parent.Count > limit)
                                {
                                    return Limit(parent.Path, $"The CQM array '{parent.Name}' contains more than {limit:N0} items.");
                                }
                            }

                            var inLiteralValue = IsInsideLiteralValue(stack, pendingProperty);
                            stack.Push(new Frame(
                                IsArray: reader.TokenType == JsonTokenType.StartArray,
                                Name: pendingProperty,
                                Path: path,
                                InLiteralValue: inLiteralValue));
                            pendingProperty = null;
                            break;
                        }

                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        if (stack.Count > 0)
                        {
                            stack.Pop();
                        }

                        pendingProperty = null;
                        break;

                    default:
                        if (stack.Count > 0 && stack.Peek().IsArray)
                        {
                            var parent = stack.Peek();
                            parent.Count += 1;
                            var limit = parent.InLiteralValue ? null : ArrayLimit(parent.Name, limits);
                            if (limit is not null && parent.Count > limit)
                            {
                                return Limit(parent.Path, $"The CQM array '{parent.Name}' contains more than {limit:N0} items.");
                            }
                        }
                        else if (stack.Count > 0 && !stack.Peek().InLiteralValue
                            && stack.Peek().Name == "page" && pendingProperty == "limit"
                            && reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var pageLimit)
                            && pageLimit > limits.MaxPageSize)
                        {
                            return Limit("$.page.limit", $"The page limit {pageLimit:N0} is above the maximum of {limits.MaxPageSize:N0}.");
                        }

                        pendingProperty = null;
                        break;
                }
            }
        }
        catch (JsonException)
        {
            // Malformed JSON is reported by JsonDocument.Parse with the established cqm.jsonInvalid shape.
            // Only the guard's own bounds produce a cqm.limitExceeded result.
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }

        return null;
    }

    // SQL7-01: `literal.value` carries caller data, not query structure. Names such as `arguments`,
    // `select` or `page.limit` inside it must not be interpreted as CQM fields; byte/token/depth
    // bounds stay global.
    private static bool IsInsideLiteralValue(Stack<Frame> stack, string? pendingProperty)
    {
        if (stack.Count == 0)
        {
            return false;
        }

        var parent = stack.Peek();
        // In the CQM expression grammar the literal form is identified by its `value` property; the
        // enclosing expression object is not named "literal" on the wire.
        return parent.InLiteralValue || pendingProperty == "value";
    }

    private static int? ArrayLimit(string? name, CqmLimits limits) => name switch
    {
        "select" => limits.MaxSelectItems,
        "joins" => limits.MaxJoins,
        "groupBy" => limits.MaxGroupByFields,
        "orderBy" => limits.MaxOrderByFields,
        "arguments" => limits.MaxArguments,
        _ => null,
    };

    private static string ValuePath(Stack<Frame> stack, string? pendingProperty)
    {
        if (stack.Count == 0)
        {
            return "$";
        }

        var parent = stack.Peek();
        if (parent.IsArray)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{parent.Path}[{parent.Count}]");
        }

        return pendingProperty is null ? parent.Path : string.Concat(parent.Path, ".", pendingProperty);
    }

    private static string CurrentPath(Stack<Frame> stack, string? pendingProperty)
        => stack.Count == 0 ? "$" : ValuePath(stack, pendingProperty);

    private static CqmValidationError Limit(string path, string message)
        => new(CqmValidationErrorCodes.LimitExceeded, path, message);

    private sealed class Frame(bool IsArray, string? Name, string Path, bool InLiteralValue)
    {
        public bool IsArray { get; } = IsArray;

        public string? Name { get; } = Name;

        public string Path { get; } = Path;

        public bool InLiteralValue { get; } = InLiteralValue;

        public int Count { get; set; }
    }
}
