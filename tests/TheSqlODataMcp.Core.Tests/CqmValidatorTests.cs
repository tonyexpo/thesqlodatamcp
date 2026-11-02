using TheSqlODataMcp.Core.Cqm;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>
/// Structural CQM validation tests for rules the JSON Schema cannot express: alias scope/uniqueness,
/// expression arity, aggregate placement/nesting, HAVING classification, and literal-zero division.
/// </summary>
public sealed class CqmValidatorTests
{
    private const string MinimalQuery = """
        {
          "version": "1.0",
          "from": { "entity": "sales.Invoices", "alias": "i" },
          "page": { "limit": 10, "offset": 0 }
        }
        """;

    [Fact]
    public void DuplicateJoinAliasIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "joins": [
                {
                  "type": "inner",
                  "source": { "entity": "sales.Customers", "alias": "i" },
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
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.DuplicateAlias && error.Path == "$.joins[0].source.alias");
    }

    [Fact]
    public void AndJoinConditionOfEqualitiesIsAccepted()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.InvoiceLines", "alias": "l" },
              "joins": [
                {
                  "type": "left",
                  "source": { "entity": "sales.Customers", "alias": "c" },
                  "on": {
                    "operator": "and",
                    "arguments": [
                      {
                        "operator": "eq",
                        "arguments": [
                          { "field": "c.Id" },
                          { "field": "l.CustomerId" }
                        ]
                      },
                      {
                        "operator": "eq",
                        "arguments": [
                          { "field": "c.TenantId" },
                          { "field": "l.TenantId" }
                        ]
                      }
                    ]
                  }
                }
              ],
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void OrJoinConditionIsRejected()
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
                    "operator": "or",
                    "arguments": [
                      {
                        "operator": "eq",
                        "arguments": [
                          { "field": "c.Id" },
                          { "field": "i.CustomerId" }
                        ]
                      },
                      {
                        "operator": "eq",
                        "arguments": [
                          { "field": "c.Id" },
                          { "field": "i.Id" }
                        ]
                      }
                    ]
                  }
                }
              ],
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.JoinConditionInvalid && error.Path == "$.joins[0].on");
    }

    [Fact]
    public void DuplicateSelectAliasIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "select": [
                { "expression": { "field": "i.Id" }, "alias": "same" },
                { "expression": { "field": "i.NetAmount" }, "alias": "same" }
              ],
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.DuplicateSelectAlias && error.Path == "$.select[1].alias");
    }

    [Fact]
    public void UnknownFieldAliasIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "select": [
                { "expression": { "field": "x.Id" }, "alias": "id" }
              ],
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.UnknownAlias && error.Path == "$.select[0].expression");
    }

    [Fact]
    public void AliasReferenceInWhereIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": { "alias": "total" },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.AliasReferenceNotAllowed && error.Path == "$.where");
    }

    [Fact]
    public void UnknownAliasReferenceInOrderByIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "orderBy": [
                { "expression": { "alias": "missing" }, "direction": "asc" }
              ],
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.AliasReferenceNotFound && error.Path == "$.orderBy[0].expression");
    }

    [Fact]
    public void OperatorArityIsValidated()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": {
                "operator": "eq",
                "arguments": [
                  { "field": "i.Id" }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.OperatorArityInvalid && error.Path == "$.where");
    }

    [Fact]
    public void VariadicInIsAccepted()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": {
                "operator": "in",
                "arguments": [
                  { "field": "i.Status" },
                  { "value": "open" },
                  { "value": "paid" }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void FunctionArityIsValidated()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "select": [
                {
                  "expression": {
                    "function": "dateTrunc",
                    "arguments": [
                      { "value": "month" }
                    ]
                  },
                  "alias": "month"
                }
              ],
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.FunctionArityInvalid && error.Path == "$.select[0].expression");
    }

    [Fact]
    public void NestedAggregateIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "select": [
                {
                  "expression": {
                    "aggregate": "sum",
                    "expression": {
                      "aggregate": "sum",
                      "expression": { "field": "i.NetAmount" }
                    }
                  },
                  "alias": "total"
                }
              ],
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.NestedAggregate && error.Path == "$.select[0].expression.expression");
    }

    [Fact]
    public void AggregateInWhereIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": { "aggregate": "count" },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.AggregateNotAllowedHere && error.Path == "$.where");
    }

    [Fact]
    public void HavingWithoutAnalyticalQueryIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "having": {
                "operator": "gt",
                "arguments": [
                  { "field": "i.NetAmount" },
                  { "value": 100 }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.HavingWithoutAnalyticalQuery && error.Path == "$.having");
    }

    [Fact]
    public void DivisionByLiteralZeroIsRejected()
    {
        var result = CqmJson.Deserialize("""
            {
              "version": "1.0",
              "from": { "entity": "sales.Invoices", "alias": "i" },
              "where": {
                "operator": "divide",
                "arguments": [
                  { "field": "i.NetAmount" },
                  { "value": 0 }
                ]
              },
              "page": { "limit": 10, "offset": 0 }
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == CqmValidationErrorCodes.DivisionByZero && error.Path == "$.where.arguments[1]");
    }

    [Fact]
    public void DirectConstructionRejectsAnInvalidFieldReferenceShape()
    {
        var query = new CqmQuery(
            new CqmEntitySource("sales.Invoices", "i"),
            new CqmPage(10, 0),
            select: [new CqmSelectItem(new CqmFieldExpression("i"), "id")]);

        var errors = CqmValidator.Validate(query);

        Assert.Contains(errors, error => error.Code == CqmValidationErrorCodes.FieldReferenceInvalid && error.Path == "$.select[0].expression");
    }

    [Fact]
    public void MinimalQueryIsValid()
    {
        var result = CqmJson.Deserialize(MinimalQuery);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
        Assert.Empty(result.Query!.Joins);
        Assert.Empty(result.Query.Select);
        Assert.Empty(result.Query.GroupBy);
    }
}
