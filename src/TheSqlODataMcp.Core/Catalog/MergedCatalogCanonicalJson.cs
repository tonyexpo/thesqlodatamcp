using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// Produces the canonical representation used to compare merged catalog snapshots, and reconstructs a
/// live <see cref="MergedCatalog"/> back from it (<see cref="Deserialize"/>). Mirrors
/// <see cref="TechnicalCatalogCanonicalJson"/>'s style and determinism guarantees: entities, fields, and
/// the discovered/configured relationship union are all sorted so that differing input enumeration order
/// never changes the serialized output or its hash, while any overlay-attributable change (a display
/// name, a description, an alias, the exposed flag, an added relationship, a different effective key)
/// does change it. That same narrow, overlay-only scope is exactly why <see cref="Deserialize"/> alone
/// cannot rebuild a full <see cref="MergedCatalog"/> — see its remarks.
/// </summary>
public static class MergedCatalogCanonicalJson
{
    private static readonly JsonSerializerOptions DeserializeOptions = new() { PropertyNameCaseInsensitive = true };

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

    /// <summary>
    /// Reconstructs a <see cref="MergedCatalog"/> from <see cref="Serialize"/> output. Unlike
    /// <see cref="TechnicalCatalogCanonicalJson.Deserialize"/>, this alone is not enough: Serialize
    /// deliberately omits everything that never affects the overlay-attributable structural hash (a
    /// field's canonical/provider type, a key, an index — see the class remarks), so each
    /// <see cref="MergedEntity.Physical"/>/<see cref="MergedField.Physical"/> is taken from
    /// <paramref name="technicalCatalog"/> instead, matched by identity/name exactly like
    /// <see cref="CatalogMerger"/> itself does — including <see cref="CatalogMerger"/>'s own
    /// catalog-version/provider guard, so an unrelated <paramref name="technicalCatalog"/> is rejected
    /// up front rather than silently supplying the wrong physical entity/field for a matching identity.
    /// </summary>
    public static MergedCatalog Deserialize(string json, TechnicalCatalog technicalCatalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(technicalCatalog);

        CatalogDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<CatalogDto>(json, DeserializeOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("The merged catalog JSON is malformed.", nameof(json), ex);
        }

        if (dto is null)
        {
            throw new ArgumentException("The merged catalog JSON did not deserialize.", nameof(json));
        }

        if (!string.Equals(dto.CatalogVersion, technicalCatalog.CatalogVersion, StringComparison.Ordinal)
            || !string.Equals(dto.Provider, technicalCatalog.Provider, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The supplied technical catalog's version/provider does not match the merged catalog JSON's.", nameof(technicalCatalog));
        }

        var physicalByIdentity = technicalCatalog.Entities.ToDictionary(entity => entity.Identity);

        return new MergedCatalog(
            dto.CatalogVersion,
            dto.Provider,
            dto.Configured,
            dto.Entities.Select(entity => ToEntity(entity, physicalByIdentity)),
            dto.Name,
            dto.Title,
            dto.Description,
            dto.Warnings.Select(warning => new SemanticOverlayWarning(warning.Title, warning.Content)),
            dto.Markdown);
    }

    private static MergedEntity ToEntity(EntityDto dto, Dictionary<PhysicalObjectIdentity, TechnicalEntity> physicalByIdentity)
    {
        var identity = new PhysicalObjectIdentity(dto.Schema, dto.Name);
        if (!physicalByIdentity.TryGetValue(identity, out var physical))
        {
            throw new ArgumentException($"No technical entity '{identity}' exists in the supplied technical catalog.", nameof(dto));
        }

        var physicalFieldsByName = physical.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);

        return new MergedEntity(
            physical,
            dto.DisplayName,
            dto.Fields.Select(field => ToField(field, physicalFieldsByName)),
            dto.Aliases,
            dto.Description,
            dto.Exposed,
            dto.OData is null ? null : new SemanticOverlayODataSettings(dto.OData.Enabled, dto.OData.EntitySetName, dto.OData.Key),
            dto.EffectiveKeyFields,
            dto.Relationships.Select(ToRelationship));
    }

    private static MergedField ToField(FieldDto dto, Dictionary<string, TechnicalField> physicalFieldsByName)
    {
        if (!physicalFieldsByName.TryGetValue(dto.Name, out var physical))
        {
            throw new ArgumentException($"No technical field '{dto.Name}' exists on the corresponding entity.", nameof(dto));
        }

        return new MergedField(physical, dto.DisplayName, dto.Description, dto.Aliases);
    }

    private static MergedRelationship ToRelationship(RelationshipDto dto) => new(
        dto.Name,
        new PhysicalObjectIdentity(dto.TargetSchema, dto.TargetName),
        dto.FieldPairs.Select(pair => new RelationshipFieldPair(pair.SourceField, pair.TargetField)),
        ToProvenance(dto.Provenance),
        FromCardinalityWireName(dto.Cardinality),
        dto.Description);

    private static RelationshipProvenance ToProvenance(string wireName) => wireName switch
    {
        "discovered" => RelationshipProvenance.Discovered,
        "configured" => RelationshipProvenance.Configured,
        _ => throw new ArgumentException($"Unknown relationship provenance '{wireName}'.", nameof(wireName)),
    };

    private static SemanticOverlayCardinality? FromCardinalityWireName(string? wireName) => wireName switch
    {
        null => null,
        "one-to-one" => SemanticOverlayCardinality.OneToOne,
        "one-to-many" => SemanticOverlayCardinality.OneToMany,
        "many-to-one" => SemanticOverlayCardinality.ManyToOne,
        "many-to-many" => SemanticOverlayCardinality.ManyToMany,
        _ => throw new ArgumentException($"Unknown relationship cardinality '{wireName}'.", nameof(wireName)),
    };

    private sealed record CatalogDto(
        string CatalogVersion,
        string Provider,
        bool Configured,
        string? Name,
        string? Title,
        string? Description,
        string? Markdown,
        WarningDto[] Warnings,
        EntityDto[] Entities);

    private sealed record WarningDto(string Title, string Content);

    private sealed record EntityDto(
        string Schema,
        string Name,
        string DisplayName,
        string? Description,
        bool Exposed,
        string[] Aliases,
        ODataDto? OData,
        string[] EffectiveKeyFields,
        FieldDto[] Fields,
        RelationshipDto[] Relationships);

    private sealed record ODataDto(bool? Enabled, string? EntitySetName, string[] Key);

    private sealed record FieldDto(string Name, int Ordinal, string? DisplayName, string? Description, string[] Aliases);

    private sealed record RelationshipDto(
        string Name,
        string Provenance,
        string TargetSchema,
        string TargetName,
        string? Cardinality,
        string? Description,
        FieldPairDto[] FieldPairs);

    private sealed record FieldPairDto(string SourceField, string TargetField);

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
