using System.Text;
using System.Text.Json.Nodes;
using TheSqlODataMcp.Core.Cqm;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>
/// Regression oracles for the 2026-11-09 independent QA findings SQL7-01/02/03: literal values are data,
/// not query structure; the configured JSON depth must be honoured after the guard; and the published
/// schema must document the default request widths and page limit.
/// </summary>
public sealed class CqmGuardContractTests
{
    [Fact]
    public void LiteralValueIsExemptFromQueryWidthAndPageLimits()
    {
        var payload = new JsonObject
        {
            ["select"] = Numbers(101),
            ["joins"] = Numbers(101),
            ["groupBy"] = Numbers(101),
            ["orderBy"] = Numbers(101),
            ["arguments"] = Numbers(101),
            ["page"] = new JsonObject { ["limit"] = 6_000, ["offset"] = 0 },
            ["literal"] = new JsonObject { ["value"] = new JsonObject { ["arguments"] = Numbers(101) } },
        };

        var query = new JsonObject
        {
            ["version"] = "1.0",
            ["from"] = new JsonObject { ["entity"] = "sales.Invoices", ["alias"] = "i" },
            ["select"] = new JsonArray(new JsonObject
            {
                ["expression"] = new JsonObject { ["value"] = payload },
                ["alias"] = "payload",
            }),
            ["page"] = new JsonObject { ["limit"] = 10, ["offset"] = 0 },
        };

        var result = CqmJson.Deserialize(query.ToJsonString());

        Assert.True(result.Succeeded, FormatErrors(result));
        var literal = Assert.IsType<CqmLiteralExpression>(Assert.Single(result.Query!.Select).Expression);
        Assert.Equal(101, literal.Value.GetProperty("arguments").GetArrayLength());
    }

    [Fact]
    public void GlobalJsonDepthStillAppliesInsideALiteralValue()
    {
        const string json = """
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "select": [
                {
                  "expression": { "value": { "a": { "b": { "c": { "d": 1 } } } } },
                  "alias": "payload"
                }
              ],
              "page": { "limit": 10, "offset": 0 }
            }
            """;

        var result = CqmJson.Deserialize(json, new CqmLimits { MaxJsonDepth = 5 });

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors);
        Assert.Equal(CqmValidationErrorCodes.LimitExceeded, error.Code);
        Assert.StartsWith("$.select[0].expression.value", error.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfiguredJsonDepthAboveTheJsonDocumentDefaultIsHonoured()
    {
        var json = NestedNot(70);

        var result = CqmJson.Deserialize(json, new CqmLimits
        {
            MaxJsonDepth = 200,
            MaxExpressionDepth = 100,
        });

        Assert.True(result.Succeeded, FormatErrors(result));
    }

    [Fact]
    public void PublishedSchemaDocumentsTheDefaultServerLimits()
    {
        var root = JsonNode.Parse(CqmJson.SchemaJson)!.AsObject();
        var properties = root["properties"]!.AsObject();
        var definitions = root["$defs"]!.AsObject();

        Assert.Equal(8, properties["joins"]!["maxItems"]!.GetValue<int>());
        Assert.Equal(1_000, properties["select"]!["maxItems"]!.GetValue<int>());
        Assert.Equal(12, properties["groupBy"]!["maxItems"]!.GetValue<int>());
        Assert.Equal(12, properties["orderBy"]!["maxItems"]!.GetValue<int>());
        Assert.Equal(100, definitions["operatorExpression"]!["properties"]!["arguments"]!["maxItems"]!.GetValue<int>());
        Assert.Equal(100, definitions["functionExpression"]!["properties"]!["arguments"]!["maxItems"]!.GetValue<int>());
        Assert.Equal(5_000, definitions["page"]!["properties"]!["limit"]!["maximum"]!.GetValue<int>());
    }

    [Fact]
    public void ParseResultCapsAnElevenThousandErrorListAndKeepsTheTotal()
    {
        var errors = Enumerable.Range(0, 11_000)
            .Select(index => new CqmValidationError(
                CqmValidationErrorCodes.SchemaViolation,
                $"$.select[{index}]",
                "Invalid CQM value."))
            .ToArray();

        var result = CqmParseResult.Failure(errors);

        Assert.Equal(CqmParseResult.MaxReportedErrors, result.Errors.Count);
        Assert.Equal(11_000, result.TotalErrors);
        Assert.True(result.Truncated);
        Assert.Equal("$.select[0]", result.Errors[0].Path);
        Assert.Equal("$.select[99]", result.Errors[^1].Path);
    }

    [Fact]
    public void ParseResultCapsReportedSchemaErrorsAndKeepsTheTotal()
    {
        var builder = new StringBuilder("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "select": [
            """);
        const int selectItems = 300;
        for (var index = 0; index < selectItems; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"expression\":{\"field\":\"i.Id\"},\"alias\":\"")
                .Append('a', 129)
                .Append(index)
                .Append("\"}");
        }

        builder.Append("],\"page\":{\"limit\":10,\"offset\":0}}");

        var result = CqmJson.Deserialize(builder.ToString());

        Assert.False(result.Succeeded);
        Assert.Equal(CqmParseResult.MaxReportedErrors, result.Errors.Count);
        Assert.True(result.TotalErrors > CqmParseResult.MaxReportedErrors);
        Assert.True(result.Truncated);
        Assert.All(result.Errors, error => Assert.Equal(CqmValidationErrorCodes.SchemaViolation, error.Code));
    }

    private static JsonArray Numbers(int count)
    {
        var array = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            array.Add(index);
        }

        return array;
    }

    private static string NestedNot(int depth)
    {
        var expression = "{\"value\":true}";
        for (var index = 0; index < depth; index++)
        {
            expression = "{\"operator\":\"not\",\"arguments\":[" + expression + "]}";
        }

        return "{\"version\":\"1.0\",\"from\":{\"entity\":\"sales.Invoices\",\"alias\":\"i\"},\"where\":"
            + expression
            + ",\"page\":{\"limit\":10,\"offset\":0}}";
    }

    private static string FormatErrors(CqmParseResult result)
        => string.Join("; ", result.Errors.Select(error => error.ToString()));
}
