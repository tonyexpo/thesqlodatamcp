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

    internal static JsonSchema Compiled { get; } = JsonSchema.FromText(Json);

    // JsonSchema.Net's Evaluate is not safe to call concurrently on a shared recursive schema instance.
    // CQM parsing may happen concurrently in a host, so serialize evaluation rather than risk a silent
    // validation bypass. Validation is cheap relative to actual query execution.
    internal static object EvaluationLock { get; } = new();

    private static string LoadJson()
    {
        using var stream = typeof(CqmSchema).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The embedded CQM v1 schema resource was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
