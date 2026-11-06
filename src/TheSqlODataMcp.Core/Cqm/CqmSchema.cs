using System.Text.Json.Nodes;
using Json.Schema;

namespace TheSqlODataMcp.Core.Cqm;

/// <summary>
/// The published v1 CQM JSON Schema. The schema is embedded in the Core assembly so every host can expose
/// the exact contract it validates, and is also available verbatim through <see cref="Json"/>.
/// </summary>
public static class CqmSchema
{
    public const string Version = CqmVersions.V1;

    private const string ResourceName = "TheSqlODataMcp.Core.Cqm.Schemas.cqm.v1.schema.json";

    public static string Json { get; } = LoadJson();

    // The published schema keeps the recursive `expression` oneOf as the client-facing contract, but
    // JsonSchema.Net evaluates every branch at every level: depth 14 costs 17 s and 3.2 GB (QA TS4-01).
    // The runtime copy therefore treats an expression as an object and lets CqmExpressionShapeValidator
    // apply the same six-form contract linearly (QA TS4-01/02, SQL-A2).
    private static readonly string RuntimeJson = BuildRuntimeSchema();

    // JsonSchema.Net's Evaluate is not safe to call concurrently on a shared schema instance (the overlay
    // importer documents an observed validation bypass). A schema instance per thread removes the shared
    // mutable evaluator without serialising every parse; the runtime schema is no longer recursive, so the
    // per-thread construction is bounded and happens once per pool thread (QA TS4-05, SQL-A3).
    private static readonly ThreadLocal<JsonSchema> Compiled = new(
        () => JsonSchema.FromText(RuntimeJson),
        trackAllValues: false);

    internal static JsonSchema GetCompiled() => Compiled.Value!;

    private static string BuildRuntimeSchema()
    {
        var root = JsonNode.Parse(Json)!.AsObject();
        var definitions = root["$defs"]!.AsObject();
        definitions["expression"] = new JsonObject
        {
            ["type"] = "object",
        };

        return root.ToJsonString();
    }

    private static string LoadJson()
    {
        using var stream = typeof(CqmSchema).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The embedded CQM v1 schema resource was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
