using System.Diagnostics;
using System.Text;
using TheSqlODataMcp.Core.Cqm;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>
/// Regression oracles for QA TS4-01/02 (SQL-A1/A2): the input boundary must reject pathological
/// documents before schema/DOM work, and expression validation must be linear in nesting depth.
/// </summary>
public sealed class CqmInputBoundaryTests
{
    [Fact]
    public void ExpressionNestingAtTheConfiguredMaximumIsLinear()
    {
        var atLimit = NestedNot(20);
        var overLimit = NestedNot(21);

        var stopwatch = Stopwatch.StartNew();
        var accepted = CqmJson.Deserialize(atLimit);
        stopwatch.Stop();

        Assert.True(accepted.Succeeded, FormatErrors(accepted));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"depth 20 took {stopwatch.Elapsed}");

        var rejected = CqmJson.Deserialize(overLimit);
        Assert.False(rejected.Succeeded);
        Assert.Contains(rejected.Errors, error => error.Code == CqmValidationErrorCodes.LimitExceeded);
    }

    [Fact]
    public void HugeSelectIsRejectedBeforeDomWork()
    {
        var json = BigSelect(100_000);
        var stopwatch = Stopwatch.StartNew();
        var result = CqmJson.Deserialize(json);
        stopwatch.Stop();

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.LimitExceeded);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"100,000 select items took {stopwatch.Elapsed}");
    }

    [Fact]
    public void CustomLimitsRejectBytesDepthAndWidths()
    {
        var bytes = CqmJson.Deserialize(EntityQuery, new CqmLimits { MaxBytes = 16 });
        Assert.Contains(bytes.Errors, error => error.Code == CqmValidationErrorCodes.LimitExceeded && error.Path == "$");

        var depth = CqmJson.Deserialize(NestedNot(5), new CqmLimits { MaxJsonDepth = 8 });
        Assert.Contains(depth.Errors, error => error.Code == CqmValidationErrorCodes.LimitExceeded);

        var select = CqmJson.Deserialize(EntityQuery, new CqmLimits { MaxSelectItems = 2 });
        Assert.Contains(select.Errors, error => error.Code == CqmValidationErrorCodes.LimitExceeded && error.Path == "$.select");

        var arguments = CqmJson.Deserialize(EntityQuery, new CqmLimits { MaxArguments = 1 });
        Assert.Contains(
            arguments.Errors,
            error => error.Code == CqmValidationErrorCodes.LimitExceeded
                && error.Path.StartsWith("$.where.arguments", StringComparison.Ordinal));
    }

    [Fact]
    public void PageLimitAboveTheMaximumIsRejectedAtTheBoundary()
    {
        var result = CqmJson.Deserialize(
            """
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "page": { "limit": 5001, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Errors,
            error => error.Code == CqmValidationErrorCodes.LimitExceeded && error.Path == "$.page.limit");
    }

    [Fact]
    public void DocumentsExactlyAtTheLimitsStillParse()
    {
        var result = CqmJson.Deserialize(
            """
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "select": [
                { "expression": { "field": "i.Id" }, "alias": "first" },
                { "expression": { "field": "i.Name" }, "alias": "second" }
              ],
              "where": {
                "operator": "eq",
                "arguments": [ { "field": "i.Id" }, { "value": 1 } ]
              },
              "page": { "limit": 5000, "offset": 0 }
            }
            """,
            new CqmLimits
            {
                MaxSelectItems = 2,
                MaxArguments = 2,
                MaxPageSize = 5_000,
            });

        Assert.True(result.Succeeded, FormatErrors(result));
    }

    [Fact]
    public void ParallelParsesMatchSequentialResults()
    {
        var expected = CqmJson.Deserialize(EntityQuery);
        Assert.True(expected.Succeeded, FormatErrors(expected));
        var expectedJson = CqmJson.Serialize(expected.Query!);

        const string invalid = """
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "select": [ { "expression": { "field": "i.Id", "value": 1 }, "alias": "bad" } ],
              "page": { "limit": 10, "offset": 0 }
            }
            """;

        var validResults = new CqmParseResult[8];
        var invalidResults = new CqmParseResult[8];
        Parallel.For(0, 8, index =>
        {
            validResults[index] = CqmJson.Deserialize(EntityQuery);
            invalidResults[index] = CqmJson.Deserialize(invalid);
        });

        Assert.All(validResults, result =>
        {
            Assert.True(result.Succeeded, FormatErrors(result));
            Assert.Equal(expectedJson, CqmJson.Serialize(result.Query!));
        });
        Assert.All(invalidResults, result => Assert.False(result.Succeeded));
    }

    [Fact]
    public void PublishedSchemaStillCarriesTheRecursiveExpressionContract()
    {
        Assert.Contains("\"oneOf\"", CqmJson.SchemaJson, StringComparison.Ordinal);
        Assert.Contains("\"$defs\"", CqmJson.SchemaJson, StringComparison.Ordinal);
    }

    private const string EntityQuery = """
        {
          "version": "1.0",
          "from": { "entity": "sales.Invoices", "alias": "i" },
          "select": [
            { "expression": { "field": "i.Id" }, "alias": "invoiceId" },
            { "expression": { "field": "i.PostingDate" }, "alias": "postingDate" },
            { "expression": { "field": "i.NetAmount" }, "alias": "netAmount" }
          ],
          "where": {
            "operator": "and",
            "arguments": [
              { "operator": "eq", "arguments": [ { "field": "i.Id" }, { "value": 1 } ] },
              { "operator": "gt", "arguments": [ { "field": "i.NetAmount" }, { "value": 10000 } ] }
            ]
          },
          "page": { "limit": 100, "offset": 0 }
        }
        """;

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

    private static string BigSelect(int count)
    {
        var builder = new StringBuilder();
        builder.Append("{\"version\":\"1.0\",\"from\":{\"entity\":\"sales.Invoices\",\"alias\":\"i\"},\"select\":[");
        for (var index = 0; index < count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"expression\":{\"field\":\"i.Id\"},\"alias\":\"a").Append(index).Append("\"}");
        }

        builder.Append("],\"page\":{\"limit\":10,\"offset\":0}}");
        return builder.ToString();
    }

    private static string FormatErrors(CqmParseResult result)
        => string.Join("; ", result.Errors.Select(error => error.ToString()));
}
