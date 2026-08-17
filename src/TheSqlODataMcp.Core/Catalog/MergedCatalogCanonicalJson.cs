using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// Produces the canonical representation used to compare merged catalog snapshots. Mirrors
/// <see cref="TechnicalCatalogCanonicalJson"/>'s style and determinism guarantees: entities, fields, and
/// the discovered/configured relationship union are all sorted so that differing input enumeration order
/// never changes the serialized output or its hash, while any overlay-attributable change (a display
/// name, a description, an alias, the exposed flag, an added relationship, a different effective key)
/// does change it.
/// </summary>
public static class MergedCatalogCanonicalJson
{
    public static string Serialize(MergedCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("catalogVersion", catalog.CatalogVersion);
            writer.WriteString("provider", catalog.Provider);
            writer.WriteBoolean("configured", catalog.Configured);
            WriteOptionalString(writer, "name", catalog.Name);
            WriteOptionalString(writer, "title", catalog.Title);
            WriteOptionalString(writer, "description", catalog.Description);
            WriteOptionalString(writer, "markdown", catalog.Markdown);

            writer.WritePropertyName("warnings");
            writer.WriteStartArray();
            foreach (var warning in catalog.Warnings)
            {
                writer.WriteStartObject();
                writer.WriteString("title", warning.Title);
                writer.WriteString("content", warning.Content);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WritePropertyName("entities");
            writer.WriteStartArray();
            foreach (var entity in catalog.Entities
                         .OrderBy(entity => entity.Physical.Identity.Schema, StringComparer.Ordinal)
                         .ThenBy(entity => entity.Physical.Identity.ObjectName, StringComparer.Ordinal))
            {
                WriteEntity(writer, entity);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string ComputeStructuralHash(MergedCatalog catalog)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(catalog)));
        return Convert.ToHexStringLower(bytes);
    }

    private static void WriteEntity(Utf8JsonWriter writer, MergedEntity entity)
    {
        writer.WriteStartObject();
        writer.WriteString("schema", entity.Physical.Identity.Schema);
        writer.WriteString("name", entity.Physical.Identity.ObjectName);
        writer.WriteString("displayName", entity.DisplayName);
        WriteOptionalString(writer, "description", entity.Description);
        writer.WriteBoolean("exposed", entity.Exposed);

        writer.WritePropertyName("aliases");
        WriteStringArray(writer, entity.Aliases);

        writer.WritePropertyName("odata");
        if (entity.OData is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartObject();
            WriteOptionalBoolean(writer, "enabled", entity.OData.Enabled);
            WriteOptionalString(writer, "entitySetName", entity.OData.EntitySetName);
            writer.WritePropertyName("key");
            WriteStringArray(writer, entity.OData.Key);
            writer.WriteEndObject();
        }

        writer.WritePropertyName("effectiveKeyFields");
        WriteStringArray(writer, entity.EffectiveKeyFields);

        writer.WritePropertyName("fields");
        writer.WriteStartArray();
        foreach (var field in entity.Fields
                     .OrderBy(field => field.Physical.Ordinal)
                     .ThenBy(field => field.Physical.Name, StringComparer.Ordinal))
        {
            WriteField(writer, field);
        }

        writer.WriteEndArray();

        writer.WritePropertyName("relationships");
        writer.WriteStartArray();
        foreach (var relationship in entity.Relationships
                     .OrderBy(relationship => relationship.Name, StringComparer.Ordinal)
                     .ThenBy(relationship => relationship.Provenance))
        {
            WriteRelationship(writer, relationship);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteField(Utf8JsonWriter writer, MergedField field)
    {
        writer.WriteStartObject();
        writer.WriteString("name", field.Physical.Name);
        writer.WriteNumber("ordinal", field.Physical.Ordinal);
        WriteOptionalString(writer, "displayName", field.DisplayName);
        WriteOptionalString(writer, "description", field.Description);
        writer.WritePropertyName("aliases");
        WriteStringArray(writer, field.Aliases);
        writer.WriteEndObject();
    }

    private static void WriteRelationship(Utf8JsonWriter writer, MergedRelationship relationship)
    {
        writer.WriteStartObject();
        writer.WriteString("name", relationship.Name);
        writer.WriteString("provenance", relationship.Provenance == RelationshipProvenance.Discovered ? "discovered" : "configured");
        writer.WriteString("targetSchema", relationship.Target.Schema);
        writer.WriteString("targetName", relationship.Target.ObjectName);
        WriteOptionalString(writer, "cardinality", ToCardinalityWireName(relationship.Cardinality));
        WriteOptionalString(writer, "description", relationship.Description);

        writer.WritePropertyName("fieldPairs");
        writer.WriteStartArray();
        foreach (var pair in relationship.FieldPairs)
        {
            writer.WriteStartObject();
            writer.WriteString("sourceField", pair.SourceField);
            writer.WriteString("targetField", pair.TargetField);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteStringArray(Utf8JsonWriter writer, IReadOnlyList<string> values)
    {
        writer.WriteStartArray();
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static void WriteOptionalString(Utf8JsonWriter writer, string name, string? value)
    {
        writer.WritePropertyName(name);
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }

    private static void WriteOptionalBoolean(Utf8JsonWriter writer, string name, bool? value)
    {
        writer.WritePropertyName(name);
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteBooleanValue(value.Value);
    }

    private static string? ToCardinalityWireName(SemanticOverlayCardinality? cardinality) => cardinality switch
    {
        null => null,
        SemanticOverlayCardinality.OneToOne => "one-to-one",
        SemanticOverlayCardinality.OneToMany => "one-to-many",
        SemanticOverlayCardinality.ManyToOne => "many-to-one",
        SemanticOverlayCardinality.ManyToMany => "many-to-many",
        _ => throw new ArgumentOutOfRangeException(nameof(cardinality), cardinality, "Unknown cardinality."),
    };
}
