using System.Text.Json;
using TheSqlODataMcp.Core.Catalog;
using TheSqlODataMcp.Core.Cqm;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>Construction-time invariant tests for the public CQM v1 types.</summary>
public sealed class CqmModelTests
{
    [Fact]
    public void JoinRequiresExactlyOneCondition()
    {
        var source = new CqmEntitySource("sales.Customers", "c");

        Assert.Throws<ArgumentException>(() => new CqmJoin(CqmJoinType.Inner, source));
        Assert.Throws<ArgumentException>(() => new CqmJoin(
            CqmJoinType.Inner,
            source,
            new CqmOperatorExpression(
                CqmOperator.Eq,
                [new CqmFieldExpression("i.CustomerId"), new CqmFieldExpression("c.Id")]),
            "i.customer"));
    }

    [Fact]
    public void AggregateRequiresAnExpressionUnlessItIsCount()
    {
        Assert.Throws<ArgumentException>(() => new CqmAggregateExpression(CqmAggregate.Sum));
        Assert.Throws<ArgumentException>(() => new CqmAggregateExpression(CqmAggregate.Count, distinct: true));
        Assert.NotNull(new CqmAggregateExpression(CqmAggregate.Count));
    }

    [Fact]
    public void LiteralRejectsAnIncompatibleDeclaredType()
    {
        using var document = JsonDocument.Parse("\"not-an-int32\"");

        Assert.Throws<ArgumentException>(() => new CqmLiteralExpression(document.RootElement, CanonicalScalarType.Int32));
    }

    [Fact]
    public void LiteralRejectsUnknownCanonicalType()
    {
        using var document = JsonDocument.Parse("1");

        Assert.Throws<ArgumentOutOfRangeException>(() => new CqmLiteralExpression(document.RootElement, CanonicalScalarType.Unknown));
    }

    [Fact]
    public void LiteralRejectsAnUnpairedUtf16Surrogate()
    {
        using var document = JsonDocument.Parse("\"\\ud800\"");

        Assert.Throws<ArgumentException>(() => new CqmLiteralExpression(document.RootElement));
    }

    [Fact]
    public void PageRejectsInvalidBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CqmPage(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CqmPage(1, -1));
    }

    [Fact]
    public void QueryRejectsAnUnsupportedVersion()
    {
        Assert.Throws<ArgumentException>(() => new CqmQuery(
            new CqmEntitySource("sales.Invoices", "i"),
            new CqmPage(10, 0),
            version: "2.0"));
    }

    [Fact]
    public void QueryCopiesCollectionsAndRejectsNullElements()
    {
        var query = new CqmQuery(
            new CqmEntitySource("sales.Invoices", "i"),
            new CqmPage(10, 0),
            select: [new CqmSelectItem(new CqmFieldExpression("i.Id"), "id")]);

        Assert.Single(query.Select);
        Assert.Throws<ArgumentException>(() => new CqmQuery(
            new CqmEntitySource("sales.Invoices", "i"),
            new CqmPage(10, 0),
            select: [null!]));
    }

    [Fact]
    public void EntitySourceRejectsUnsafeAliases()
    {
        Assert.ThrowsAny<ArgumentException>(() => new CqmEntitySource("sales.Invoices", "1bad"));
        Assert.ThrowsAny<ArgumentException>(() => new CqmEntitySource("sales.Invoices", "bad-alias"));
        Assert.ThrowsAny<ArgumentException>(() => new CqmEntitySource(" ", "i"));
    }

    [Fact]
    public void ReferenceStringsRejectUnpairedUtf16Surrogates()
    {
        var invalid = "sales." + (char)0xD800;

        Assert.ThrowsAny<ArgumentException>(() => new CqmEntitySource(invalid, "i"));
        Assert.ThrowsAny<ArgumentException>(() => new CqmFieldExpression("i." + (char)0xD800));
        Assert.ThrowsAny<ArgumentException>(() => new CqmJoin(
            CqmJoinType.Inner,
            new CqmEntitySource("sales.Customers", "c"),
            relationship: "r" + (char)0xD800));
    }

    [Fact]
    public void OperatorAndFunctionRejectEmptyArgumentLists()
    {
        Assert.Throws<ArgumentException>(() => new CqmOperatorExpression(CqmOperator.Eq, []));
        Assert.Throws<ArgumentException>(() => new CqmFunctionExpression(CqmFunction.Lower, []));
    }

    [Fact]
    public void AnalyticalQueryRoundTripsWithJoinsAndAggregate()
    {
        using var zero = JsonDocument.Parse("0");
        var query = new CqmQuery(
            new CqmEntitySource("sales.InvoiceLines", "l"),
            new CqmPage(25, 50),
            joins:
            [
                new CqmJoin(
                    CqmJoinType.Left,
                    new CqmEntitySource("sales.Customers", "c"),
                    relationship: "l.customer"),
            ],
            select:
            [
                new CqmSelectItem(new CqmAggregateExpression(CqmAggregate.Sum, new CqmFieldExpression("l.UnitPrice")), "total"),
            ],
            groupBy: [new CqmFieldExpression("l.InvoiceId")],
            having: new CqmOperatorExpression(
                CqmOperator.Gt,
                [new CqmAliasExpression("total"), new CqmLiteralExpression(zero.RootElement)]),
            orderBy: [new CqmOrderBy(new CqmAliasExpression("total"), CqmSortDirection.Desc)],
            distinct: true,
            includeTotalCount: true);

        var json = CqmJson.Serialize(query);
        var result = CqmJson.Deserialize(json);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal(json, CqmJson.Serialize(result.Query!));
        Assert.Equal(25, result.Query!.Page.Limit);
        Assert.Equal(50, result.Query.Page.Offset);
        Assert.True(result.Query.Distinct);
        Assert.True(result.Query.IncludeTotalCount);
        var join = Assert.Single(result.Query.Joins);
        Assert.Equal(CqmJoinType.Left, join.Type);
        Assert.Equal("l.customer", join.Relationship);
        Assert.Single(result.Query.GroupBy);
        Assert.NotNull(result.Query.Having);
        Assert.Equal(CqmSortDirection.Desc, Assert.Single(result.Query.OrderBy).Direction);
        Assert.Equal(CqmAggregate.Sum, Assert.IsType<CqmAggregateExpression>(Assert.Single(result.Query.Select).Expression).Aggregate);
    }
}
