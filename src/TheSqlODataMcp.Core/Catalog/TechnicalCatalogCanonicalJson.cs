using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// Produces the canonical representation used to compare technical catalog snapshots.
/// </summary>
public static class TechnicalCatalogCanonicalJson
{
    public static string Serialize(TechnicalCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("catalogVersion", catalog.CatalogVersion);
            writer.WriteString("provider", catalog.Provider);
            writer.WritePropertyName("entities");
            writer.WriteStartArray();
            foreach (var entity in catalog.Entities
                         .OrderBy(entity => entity.Identity.Schema, StringComparer.Ordinal)
                         .ThenBy(entity => entity.Identity.ObjectName, StringComparer.Ordinal))
            {
                WriteEntity(writer, entity);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string ComputeStructuralHash(TechnicalCatalog catalog)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(catalog)));
        return Convert.ToHexStringLower(bytes);
    }

    private static void WriteEntity(Utf8JsonWriter writer, TechnicalEntity entity)
    {
        writer.WriteStartObject();
        writer.WriteString("schema", entity.Identity.Schema);
        writer.WriteString("name", entity.Identity.ObjectName);
        writer.WriteString("kind", entity.Kind == CatalogObjectKind.Table ? "table" : "view");
        WriteOptionalString(writer, "description", entity.Description);
        writer.WriteBoolean("isTemporal", entity.IsTemporal);
        writer.WritePropertyName("fields");
        writer.WriteStartArray();
        foreach (var field in entity.Fields.OrderBy(field => field.Ordinal).ThenBy(field => field.Name, StringComparer.Ordinal))
        {
            WriteField(writer, field);
        }

        writer.WriteEndArray();
        writer.WritePropertyName("keys");
        writer.WriteStartArray();
        foreach (var key in entity.Keys.OrderBy(key => key.Name, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("name", key.Name);
            writer.WriteBoolean("isPrimary", key.IsPrimary);
            WriteStringArray(writer, "fields", key.Fields);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WritePropertyName("indexes");
        writer.WriteStartArray();
        foreach (var index in entity.Indexes.OrderBy(index => index.Name, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("name", index.Name);
            writer.WriteBoolean("isUnique", index.IsUnique);
            writer.WriteBoolean("isFiltered", index.IsFiltered);
            WriteOptionalString(writer, "description", index.Description);
            WriteStringArray(writer, "fields", index.Fields);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WritePropertyName("relationships");
        writer.WriteStartArray();
        foreach (var relationship in entity.Relationships.OrderBy(relationship => relationship.Name, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("name", relationship.Name);
            writer.WriteString("targetSchema", relationship.Target.Schema);
            writer.WriteString("targetName", relationship.Target.ObjectName);
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

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteField(Utf8JsonWriter writer, TechnicalField field)
    {
        writer.WriteStartObject();
        writer.WriteString("name", field.Name);
        writer.WriteNumber("ordinal", field.Ordinal);
        writer.WriteString("canonicalType", ToWireName(field.CanonicalType));
        writer.WritePropertyName("providerType");
        writer.WriteStartObject();
        writer.WriteString("name", field.ProviderType.Name);
        writer.WriteString("storeRepresentation", field.ProviderType.StoreRepresentation);
        WriteOptionalNumber(writer, "length", field.ProviderType.Length);
        WriteOptionalNumber(writer, "precision", field.ProviderType.Precision);
        WriteOptionalNumber(writer, "scale", field.ProviderType.Scale);
        writer.WriteEndObject();
        writer.WriteBoolean("isNullable", field.IsNullable);
        WriteOptionalString(writer, "description", field.Description);
        writer.WriteBoolean("isIdentity", field.IsIdentity);
        writer.WriteBoolean("isComputed", field.IsComputed);
        writer.WriteBoolean("isPersistedComputed", field.IsPersistedComputed);
        writer.WriteBoolean("isTemporalPeriodStart", field.IsTemporalPeriodStart);
        writer.WriteBoolean("isTemporalPeriodEnd", field.IsTemporalPeriodEnd);
        writer.WriteBoolean("isRowVersion", field.IsRowVersion);
        writer.WriteEndObject();
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

    private static void WriteOptionalNumber(Utf8JsonWriter writer, string name, int? value)
    {
        writer.WritePropertyName(name);
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteNumberValue(value.Value);
    }

    private static void WriteStringArray(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static string ToWireName(CanonicalScalarType type) => type switch
    {
        CanonicalScalarType.Boolean => "boolean",
        CanonicalScalarType.Int16 => "int16",
        CanonicalScalarType.Int32 => "int32",
        CanonicalScalarType.Int64 => "int64",
        CanonicalScalarType.Decimal => "decimal",
        CanonicalScalarType.Double => "double",
        CanonicalScalarType.String => "string",
        CanonicalScalarType.Guid => "guid",
        CanonicalScalarType.Date => "date",
        CanonicalScalarType.Time => "time",
        CanonicalScalarType.DateTime => "datetime",
        CanonicalScalarType.DateTimeOffset => "datetimeOffset",
        CanonicalScalarType.Binary => "binary",
        CanonicalScalarType.Json => "json",
        CanonicalScalarType.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown canonical scalar type."),
    };
}
