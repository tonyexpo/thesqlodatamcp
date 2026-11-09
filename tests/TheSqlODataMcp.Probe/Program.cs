using System.Text;
using TheSqlODataMcp.Core.Catalog;
using TheSqlODataMcp.Core.Cqm;

// Resource oracles for SQL-A5. Each scenario runs in its own process so a regression to a recursive or
// exponential path fails under the parent's memory/time budget instead of hanging or killing the test host.
if (args.Length != 1)
{
    Console.Error.WriteLine("usage: TheSqlODataMcp.Probe <cqm-nested-not|cqm-big-select|overlay-flow>");
    return 2;
}

try
{
    var line = args[0] switch
    {
        "cqm-nested-not" => Describe(CqmJson.Deserialize(NestedNot(20))),
        "cqm-big-select" => Describe(CqmJson.Deserialize(BigSelect(100_000))),
        "overlay-flow" => DescribeOverlay(SemanticOverlayImporter.ImportYamlAndMarkdown(
            new string('[', 400_000),
            string.Empty,
            new TechnicalCatalog("1.0", "sqlserver", Array.Empty<TechnicalEntity>()))),
        _ => throw new ArgumentException($"Unknown probe scenario '{args[0]}'."),
    };

    Console.WriteLine(line);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 2;
}

static string Describe(CqmParseResult result) => result.Succeeded
    ? "result=success"
    : $"result=failure;code={result.Errors[0].Code};total={result.TotalErrors}";

static string DescribeOverlay(SemanticOverlayImportResult result) => result.Succeeded
    ? "result=success"
    : $"result=failure;code={result.Errors[0].Code};total={result.TotalErrors}";

static string NestedNot(int depth)
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

static string BigSelect(int count)
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
