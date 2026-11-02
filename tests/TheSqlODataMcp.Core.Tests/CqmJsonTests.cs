using System.Text.Json.Nodes;
using Json.Schema;
using TheSqlODataMcp.Core.Cqm;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>Contract tests for the published CQM v1 schema and its strict JSON entry point.</summary>
public sealed class CqmJsonTests
{
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
              {
                "operator": "ge",
                "arguments": [
                  { "field": "i.PostingDate" },
                  { "value": "2026-01-01", "type": "date" }
                ]
              },
              {
                "operator": "gt",
                "arguments": [
                  { "field": "i.NetAmount" },
                  { "value": 10000 }
                ]
              }
            ]
          },
          "orderBy": [
            { "expression": { "field": "i.NetAmount" }, "direction": "desc" }
          ],
          "page": { "limit": 100, "offset": 0 }
        }
        """;

    private const string AnalyticalQuery = """
        {
          "version": "1.0",
          "from": { "entity": "sales.InvoiceLines", "alias": "l" },
          "joins": [
            {
              "type": "inner",
              "source": { "entity": "sales.Invoices", "alias": "i" },
              "on": {
                "operator": "eq",
                "arguments": [
                  { "field": "l.InvoiceId" },
                  { "field": "i.Id" }
                ]
              }
            },
            {
              "type": "left",
              "source": { "entity": "sales.Customers", "alias": "c" },
              "on": {
                "operator": "eq",
                "arguments": [
                  { "field": "i.CustomerId" },
                  { "field": "c.Id" }
                ]
              }
            }
          ],
          "select": [
            {
              "expression": {
                "function": "dateTrunc",
                "arguments": [
                  { "value": "month" },
                  { "field": "i.PostingDate" }
                ]
              },
              "alias": "month"
            },
            {
              "expression": { "field": "c.Country" },
              "alias": "country"
            },
            {
              "expression": {
                "aggregate": "sum",
                "expression": {
                  "operator": "multiply",
                  "arguments": [
                    { "field": "l.Quantity" },
                    { "field": "l.UnitPrice" }
                  ]
                }
              },
              "alias": "totalAmount"
            }
          ],
          "where": {
            "operator": "ge",
            "arguments": [
              { "field": "i.PostingDate" },
              { "value": "2026-01-01", "type": "date" }
            ]
          },
          "groupBy": [
            {
              "function": "dateTrunc",
              "arguments": [
                { "value": "month" },
                { "field": "i.PostingDate" }
              ]
            },
            { "field": "c.Country" }
          ],
          "having": {
            "operator": "gt",
            "arguments": [
              { "alias": "totalAmount" },
              { "value": 100000 }
            ]
          },
          "orderBy": [
            { "expression": { "alias": "totalAmount" }, "direction": "desc" }
          ],
          "page": { "limit": 100, "offset": 0 }
        }
        """;

    [Fact]
    public void SchemaIsPublishedAndParsesAsJsonSchema()
    {
        Assert.Equal("1.0", CqmJson.SchemaVersion);
        Assert.Contains("\"$defs\"", CqmJson.SchemaJson, StringComparison.Ordinal);
        Assert.NotNull(JsonSchema.FromText(CqmJson.SchemaJson));
    }

    [Fact]
    public void ParsesTheHandoffEntityQuery()
    {
        var result = CqmJson.Deserialize(EntityQuery);

        Assert.True(result.Succeeded, FormatErrors(result));
        var query = result.Query!;
        Assert.Equal("1.0", query.Version);
        Assert.Equal("sales.Invoices", query.From.Entity);
        Assert.Equal("i", query.From.Alias);
        Assert.Equal(3, query.Select.Count);
        var where = Assert.IsType<CqmOperatorExpression>(query.Where);
        Assert.Equal(CqmOperator.And, where.Operator);
        Assert.Equal(2, where.Arguments.Count);
        Assert.Equal(CqmSortDirection.Desc, Assert.Single(query.OrderBy).Direction);
        Assert.Equal(100, query.Page.Limit);
        Assert.Equal(0, query.Page.Offset);
    }

    [Fact]
    public void ParsesTheHandoffAnalyticalQuery()
    {
        var result = CqmJson.Deserialize(AnalyticalQuery);

        Assert.True(result.Succeeded, FormatErrors(result));
        var query = result.Query!;
        Assert.Equal(2, query.Joins.Count);
        Assert.Equal(CqmJoinType.Inner, query.Joins[0].Type);
        Assert.Equal(CqmJoinType.Left, query.Joins[1].Type);
        Assert.Equal(3, query.Select.Count);
        Assert.Equal(2, query.GroupBy.Count);
        Assert.NotNull(query.Having);
        var aggregate = Assert.IsType<CqmAggregateExpression>(query.Select[2].Expression);
        Assert.Equal(CqmAggregate.Sum, aggregate.Aggregate);
        var multiply = Assert.IsType<CqmOperatorExpression>(aggregate.Expression);
        Assert.Equal(CqmOperator.Multiply, multiply.Operator);
    }

    [Fact]
    public void SerializesAndReparsesTheEntityQuery()
    {
        var first = CqmJson.Deserialize(EntityQuery);
        Assert.True(first.Succeeded, FormatErrors(first));

        var serialized = CqmJson.Serialize(first.Query!);

        Assert.Contains("\"from\"", serialized, StringComparison.Ordinal);
        Assert.Contains("\"postingDate\"", serialized, StringComparison.Ordinal);
        Assert.Contains("\"date\"", serialized, StringComparison.Ordinal);
        var second = CqmJson.Deserialize(serialized);
        Assert.True(second.Succeeded, FormatErrors(second));
        Assert.Equal(serialized, CqmJson.Serialize(second.Query!));
        Assert.Equal(first.Query!.From.Entity, second.Query!.From.Entity);
        Assert.Equal(first.Query.Select.Count, second.Query.Select.Count);
        Assert.Equal(first.Query.Page.Limit, second.Query.Page.Limit);
        Assert.Equal(CqmSortDirection.Desc, Assert.Single(second.Query.OrderBy).Direction);
        Assert.Equal(3, second.Query.Select.Count);
    }

    [Fact]
    public void DuplicateJsonPropertyIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.JsonInvalid && error.Path == "$.version");
    }

    [Fact]
    public void ValidHugeDoubleAndJsonLiteralsAreAccepted()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": {
                "operator": "eq",
                "arguments": [
                  { "field": "i.Id" },
                  { "value": 1e29, "type": "double" }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.True(result.Succeeded, FormatErrors(result));
        Assert.True(CqmJson.Deserialize(CqmJson.Serialize(result.Query!)).Succeeded);

        var json = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": {
                "operator": "eq",
                "arguments": [
                  { "field": "i.Payload" },
                  { "value": { "huge": 1e100 }, "type": "json" }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.True(json.Succeeded, FormatErrors(json));
        Assert.True(CqmJson.Deserialize(CqmJson.Serialize(json.Query!)).Succeeded);
    }

    [Fact]
    public void OutOfDecimalRangeRootNumberIsAStructuredFailure()
    {
        var result = CqmJson.Deserialize("1e29");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.SchemaViolation && error.Path == "$");
    }

    [Fact]
    public void OutOfDecimalRangeInt16IsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": {
                "operator": "eq",
                "arguments": [
                  { "field": "i.Id" },
                  { "value": 1e29, "type": "int16" }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.DeserializationFailed);
    }

    [Fact]
    public void DoubleOverflowIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": {
                "operator": "eq",
                "arguments": [
                  { "field": "i.Amount" },
                  { "value": 1e400, "type": "double" }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.DeserializationFailed);
    }

    [Fact]
    public void DoubleUnderflowIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": {
                "operator": "eq",
                "arguments": [
                  { "field": "i.Amount" },
                  { "value": 1e-400, "type": "double" }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.DeserializationFailed);
    }

    [Fact]
    public void SubnormalDoubleIsAccepted()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": {
                "operator": "eq",
                "arguments": [
                  { "field": "i.Amount" },
                  { "value": 5e-324, "type": "double" }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.True(result.Succeeded, FormatErrors(result));
    }

    [Fact]
    public void UntypedUnderflowIsNotClassifiedAsLiteralZero()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": {
                "operator": "divide",
                "arguments": [
                  { "field": "i.Amount" },
                  { "value": 1e-400 }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.True(result.Succeeded, FormatErrors(result));
    }

    [Fact]
    public void RawUnpairedSurrogateIsAStructuredFailure()
    {
        var json = "{\"version\":\"1.0\",\"from\":{\"entity\":\"sales.Invoices\",\"alias\":\"i\"},\"page\":{\"limit\":10,\"offset\":0},\"where\":{\"operator\":\"eq\",\"arguments\":[{\"field\":\"i.Id\"},{\"value\":\"\uD800\"}]}}";

        var result = CqmJson.Deserialize(json);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.JsonInvalid && error.Path == "$");
    }

    [Fact]
    public void EscapedUnpairedSurrogateIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": {
                "operator": "eq",
                "arguments": [
                  { "field": "i.Id" },
                  { "value": "\ud800" }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.JsonInvalid && error.Path == "$.where.arguments[1].value");
    }

    [Fact]
    public void NullDocumentIsASchemaViolation()
    {
        var result = CqmJson.Deserialize("null");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.SchemaViolation && error.Path == "$");
    }

    [Fact]
    public void EmptyInputIsAStructuredFailure()
    {
        var result = CqmJson.Deserialize(" ");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.JsonInvalid && error.Path == "$");
    }

    [Fact]
    public void FractionalPageLimitIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "page": { "limit": 10.0, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.DeserializationFailed && error.Path == "$.page.limit");
    }

    [Fact]
    public void Int16OutOfRangeIsRejectedByTheSchemaWithoutBranchNoise()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": {
                "operator": "eq",
                "arguments": [
                  { "field": "i.Id" },
                  { "value": -32769, "type": "int16" }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors);
        Assert.Equal(CqmValidationErrorCodes.SchemaViolation, error.Code);
        Assert.Equal("$.where.arguments[1].value", error.Path);
    }

    [Fact]
    public void PageLimitOverflowDoesNotProduceAliasDiagnostics()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "page": { "limit": 2147483648, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors);
        Assert.Equal(CqmValidationErrorCodes.SchemaViolation, error.Code);
        Assert.Equal("$.page.limit", error.Path);
    }

    [Fact]
    public void WhitespaceEntityIsRejectedByTheSchema()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "   ", "alias": "i" },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.SchemaViolation && error.Path == "$.from.entity");
    }

    [Fact]
    public void AliasWithTrailingNewlineIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i\n" },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors);
        Assert.Equal(CqmValidationErrorCodes.SchemaViolation, error.Code);
        Assert.Equal("$.from.alias", error.Path);
    }

    [Fact]
    public void InvalidDateLiteralIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": {
                "operator": "eq",
                "arguments": [
                  { "field": "i.PostingDate" },
                  { "value": "not-a-date", "type": "date" }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.DeserializationFailed);
    }

    [Fact]
    public void JoinOnALiteralIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "joins": [
                {
                  "type": "inner",
                  "source": { "entity": "sales.Customers", "alias": "c" },
                  "on": { "value": 1 }
                }
              ],
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.JoinConditionInvalid && error.Path == "$.joins[0].on");
    }

    [Fact]
    public void JoinConditionMustReferenceTheJoinedAlias()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "joins": [
                {
                  "type": "inner",
                  "source": { "entity": "sales.Customers", "alias": "c" },
                  "on": {
                    "operator": "eq",
                    "arguments": [
                      { "field": "i.Id" },
                      { "field": "i.Id" }
                    ]
                  }
                }
              ],
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.JoinConditionMissingSourceAlias && error.Path == "$.joins[0].on");
    }

    [Fact]
    public void HavingWithAnAggregateIsAnalytical()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "having": {
                "operator": "gt",
                "arguments": [
                  { "aggregate": "count" },
                  { "value": 1 }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.True(result.Succeeded, FormatErrors(result));
    }

    [Fact]
    public void ConcurrentParsesDoNotBypassSchemaValidation()
    {
        const string invalid = """
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "bogus": true,
              "page": { "limit": 10, "offset": 0 }
            }
            """;

        var accepted = 0;
        Parallel.For(0, 200, new ParallelOptions { MaxDegreeOfParallelism = 16 }, _ =>
        {
            if (CqmJson.Deserialize(invalid).Succeeded)
            {
                Interlocked.Increment(ref accepted);
            }
        });

        Assert.Equal(0, accepted);
    }

    [Fact]
    public void MalformedJsonReturnsAStableError()
    {
        var result = CqmJson.Deserialize("{");

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors);
        Assert.Equal(CqmValidationErrorCodes.JsonInvalid, error.Code);
        Assert.Equal("$", error.Path);
    }

    [Fact]
    public void UnknownRootPropertyIsRejectedByTheSchema()
    {
        var result = CqmJson.Deserialize(EntityQuery.Replace(
            "\"version\": \"1.0\",",
            "\"version\": \"1.0\", \"bogus\": true,",
            StringComparison.Ordinal));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.SchemaViolation && error.Path == "$.bogus");
    }

    [Fact]
    public void MissingPageIsRejectedByTheSchema()
    {
        var document = JsonNode.Parse(EntityQuery)!.AsObject();
        document.Remove("page");

        var result = CqmJson.Deserialize(document.ToJsonString());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.SchemaViolation);
    }

    [Fact]
    public void ExpressionWithTwoFormsIsRejectedByTheSchema()
    {
        var result = CqmJson.Deserialize(EntityQuery.Replace(
            "{ \"field\": \"i.Id\" }",
            "{ \"field\": \"i.Id\", \"value\": 1 }",
            StringComparison.Ordinal));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.SchemaViolation);
    }

    [Fact]
    public void UnsupportedVersionIsRejectedByTheSchema()
    {
        var result = CqmJson.Deserialize(EntityQuery.Replace(
            "\"version\": \"1.0\"",
            "\"version\": \"2.0\"",
            StringComparison.Ordinal));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.SchemaViolation && error.Path == "$.version");
    }

    [Fact]
    public void NonCountAggregateWithoutExpressionIsRejectedByTheSchema()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "select": [
                { "expression": { "aggregate": "sum" }, "alias": "total" }
              ],
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.SchemaViolation && error.Path == "$.select[0].expression");
    }

    [Fact]
    public void InvalidOperatorNameIsRejectedByTheSchema()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": { "operator": "bogus", "arguments": [ { "field": "i.Id" } ] },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.SchemaViolation && error.Path == "$.where.operator");
    }

    [Fact]
    public void InvalidSortDirectionIsRejectedByTheSchema()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "orderBy": [
                { "expression": { "field": "i.Id" }, "direction": "sideways" }
              ],
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.SchemaViolation && error.Path == "$.orderBy[0].direction");
    }

    [Fact]
    public void LiteralTypeMismatchIsRejectedByTheSchema()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": {
                "operator": "eq",
                "arguments": [
                  { "field": "i.Id" },
                  { "value": "not-an-integer", "type": "int32" }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.SchemaViolation && error.Path == "$.where.arguments[1].value");
    }

    [Fact]
    public void PageMustBeBounded()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "page": { "limit": 0, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.SchemaViolation && error.Path == "$.page.limit");
    }

    [Fact]
    public void AliasMustBeSafe()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "bad-alias" },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.SchemaViolation && error.Path == "$.from.alias");
    }

    [Fact]
    public void JoinWithNeitherOnNorRelationshipRepresentsAutomaticResolution()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Secrets", "alias": "s" },
              "joins": [
                {
                  "type": "inner",
                  "source": { "entity": "sales.Other", "alias": "o" }
                }
              ],
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.True(result.Succeeded, FormatErrors(result));
        var join = Assert.Single(result.Query!.Joins);
        Assert.Null(join.On);
        Assert.Null(join.Relationship);
    }

    [Theory]
    [InlineData("time", "12:30:00", true)]
    [InlineData("time", "12:30:00.1", true)]
    [InlineData("time", "12:30:00.1234567", true)]
    [InlineData("time", "12:30:00.", false)]
    [InlineData("time", "November 2", false)]
    [InlineData("time", "2026-11-02T12:30:00", false)]
    [InlineData("time", "12:30", false)]
    [InlineData("datetime", "2026-11-02T12:30:00", true)]
    [InlineData("datetime", "2026-11-02T12:30:00.1", true)]
    [InlineData("datetime", "2026-11-02T12:30:00.1234567", true)]
    [InlineData("datetime", "2026-11-02T12:30:00.", false)]
    [InlineData("datetime", "12:30", false)]
    [InlineData("datetime", "2026-11-02", false)]
    [InlineData("datetime", "2026-11-02T12:30:00Z", false)]
    [InlineData("datetimeOffset", "2026-11-02T12:30:00Z", true)]
    [InlineData("datetimeOffset", "2026-11-02T12:30:00.1Z", true)]
    [InlineData("datetimeOffset", "2026-11-02T12:30:00.1234567+02:00", true)]
    [InlineData("datetimeOffset", "2026-11-02T12:30:00.Z", false)]
    [InlineData("datetimeOffset", "2026-11-02T12:30:00.+02:00", false)]
    [InlineData("datetimeOffset", "12:30", false)]
    [InlineData("datetimeOffset", "2026-11-02T12:30:00", false)]
    [InlineData("datetimeOffset", "2026-11-02T12:30:00.123456789Z", false)]
    public void TemporalLiteralsUseACompleteDeterministicGrammar(string type, string value, bool accepted)
    {
        var result = CqmJson.Deserialize($$"""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": {
                "operator": "eq",
                "arguments": [
                  { "field": "i.Id" },
                  { "value": "{{value}}", "type": "{{type}}" }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.Equal(accepted, result.Succeeded);
        if (!accepted)
        {
            Assert.Contains(
                result.Errors,
                error => error.Code is CqmValidationErrorCodes.DeserializationFailed or CqmValidationErrorCodes.SchemaViolation);
        }
    }

    [Fact]
    public void JoinWithBothOnAndRelationshipIsRejectedByTheSchema()
    {
        const string query = """
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "joins": [
                {
                  "type": "inner",
                  "source": { "entity": "sales.Customers", "alias": "c" },
                  "on": {
                    "operator": "eq",
                    "arguments": [
                      { "field": "i.CustomerId" },
                      { "field": "c.Id" }
                    ]
                  },
                  "relationship": "i.customer"
                }
              ],
              "page": { "limit": 10, "offset": 0 }
            }
            """;

        var result = CqmJson.Deserialize(query);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.SchemaViolation);
    }

    private static string FormatErrors(CqmParseResult result) =>
        string.Join(Environment.NewLine, result.Errors.Select(error => error.ToString()));
}
