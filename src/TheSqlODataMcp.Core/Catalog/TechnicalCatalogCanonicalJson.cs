using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// Produces the canonical representation used to compare technical catalog snapshots, and reconstructs a
/// live <see cref="TechnicalCatalog"/> back from that representation (<see cref="Deserialize"/>) — the
/// control store's own persisted format is exactly this JSON (<c>StoredCatalogRevision.TechnicalCatalogJson</c>),
/// so this is also how a stored revision becomes usable again without re-introspecting the source.
/// </summary>
public static class TechnicalCatalogCanonicalJson
{
    private static readonly JsonSerializerOptions DeserializeOptions = new() { PropertyNameCaseInsensitive = true };

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

    /// <summary>
    /// Reconstructs a <see cref="TechnicalCatalog"/> from <see cref="Serialize"/> output. Every domain
    /// object is rebuilt through its own public constructor, so the same invariants Serialize's input
    /// already satisfied are re-checked on the way back in.
    /// </summary>
    public static TechnicalCatalog Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        CatalogDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<CatalogDto>(json, DeserializeOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("The technical catalog JSON is malformed.", nameof(json), ex);
        }

        if (dto is null)
        {
            throw new ArgumentException("The technical catalog JSON did not deserialize.", nameof(json));
        }

        return new TechnicalCatalog(dto.CatalogVersion, dto.Provider, dto.Entities.Select(ToEntity));
    }

    private static TechnicalEntity ToEntity(EntityDto dto) => new(
        new PhysicalObjectIdentity(dto.Schema, dto.Name),
        ToKind(dto.Kind),
        dto.Fields.Select(ToField),
        dto.Keys.Select(key => new CatalogKey(key.Name, key.Fields, key.IsPrimary)),
        dto.Indexes.Select(index => new CatalogIndex(index.Name, index.Fields, index.IsUnique, index.Description, index.IsFiltered)),
        dto.Relationships.Select(ToRelationship),
        dto.Description,
        dto.IsTemporal);

    private static TechnicalField ToField(FieldDto dto)
    {
        if (dto.ProviderType is null)
        {
            throw new ArgumentException($"Field '{dto.Name}' is missing its provider type.", nameof(dto));
        }

        return new(
            dto.Name,
            dto.Ordinal,
            FromWireName(dto.CanonicalType),
            new ProviderTypeDetails(dto.ProviderType.Name, dto.ProviderType.StoreRepresentation, dto.ProviderType.Length, dto.ProviderType.Precision, dto.ProviderType.Scale),
            dto.IsNullable,
            dto.Description,
            dto.IsIdentity,
            dto.IsComputed,
            dto.IsPersistedComputed,
            dto.IsTemporalPeriodStart,
            dto.IsTemporalPeriodEnd,
            dto.IsRowVersion);
    }

    private static CatalogRelationship ToRelationship(RelationshipDto dto) => new(
        dto.Name,
        new PhysicalObjectIdentity(dto.TargetSchema, dto.TargetName),
        dto.FieldPairs.Select(pair => new RelationshipFieldPair(pair.SourceField, pair.TargetField)),
        dto.Description);

    private static CatalogObjectKind ToKind(string wireName) => wireName switch
    {
        "table" => CatalogObjectKind.Table,
        "view" => CatalogObjectKind.View,
        _ => throw new ArgumentException($"Unknown catalog object kind '{wireName}'.", nameof(wireName)),
    };

    private static CanonicalScalarType FromWireName(string wireName) => wireName switch
    {
        "boolean" => CanonicalScalarType.Boolean,
        "int16" => CanonicalScalarType.Int16,
        "int32" => CanonicalScalarType.Int32,
        "int64" => CanonicalScalarType.Int64,
        "decimal" => CanonicalScalarType.Decimal,
        "double" => CanonicalScalarType.Double,
        "string" => CanonicalScalarType.String,
        "guid" => CanonicalScalarType.Guid,
        "date" => CanonicalScalarType.Date,
        "time" => CanonicalScalarType.Time,
        "datetime" => CanonicalScalarType.DateTime,
        "datetimeOffset" => CanonicalScalarType.DateTimeOffset,
        "binary" => CanonicalScalarType.Binary,
        "json" => CanonicalScalarType.Json,
        "unknown" => CanonicalScalarType.Unknown,
        _ => throw new ArgumentException($"Unknown canonical scalar wire name '{wireName}'.", nameof(wireName)),
    };

    private sealed record CatalogDto(string CatalogVersion, string Provider, EntityDto[] Entities);

    private sealed record EntityDto(
        string Schema,
        string Name,
        string Kind,
        string? Description,
        bool IsTemporal,
        FieldDto[] Fields,
        KeyDto[] Keys,
        IndexDto[] Indexes,
        RelationshipDto[] Relationships);

    private sealed record FieldDto(
        string Name,
        int Ordinal,
        string CanonicalType,
        ProviderTypeDto ProviderType,
        bool IsNullable,
        string? Description,
        bool IsIdentity,
        bool IsComputed,
        bool IsPersistedComputed,
        bool IsTemporalPeriodStart,
        bool IsTemporalPeriodEnd,
        bool IsRowVersion);

    private sealed record ProviderTypeDto(string Name, string StoreRepresentation, int? Length, int? Precision, int? Scale);

    private sealed record KeyDto(string Name, bool IsPrimary, string[] Fields);

    private sealed record IndexDto(string Name, bool IsUnique, bool IsFiltered, string? Description, string[] Fields);

    private sealed record RelationshipDto(string Name, string TargetSchema, string TargetName, string? Description, FieldPairDto[] FieldPairs);

    private sealed record FieldPairDto(string SourceField, string TargetField);

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
