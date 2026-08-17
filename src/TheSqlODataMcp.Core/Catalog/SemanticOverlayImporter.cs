using System.Globalization;
using System.Text.Json.Nodes;
using Json.Schema;
using Markdig;
using Markdig.Extensions.Yaml;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// Imports and validates an administrator-authored semantic catalog overlay (Markdown narrative plus
/// YAML metadata, per the v1 schema) against a previously discovered <see cref="TechnicalCatalog"/>.
/// </summary>
/// <remarks>
/// Two independent strictness controls are required together and both run on every import:
/// strict typed YAML deserialization (rejects unrecognized keys, including the six forbidden
/// top-level sections) and versioned JSON Schema evaluation (catches structural/cross-field rules
/// that typed deserialization alone cannot express, such as required fields and enum values).
/// Neither stage stops the other from running, and physical-reference validation against the
/// supplied <see cref="TechnicalCatalog"/> runs whenever a typed document is available, so a single
/// import call collects every independently detectable problem rather than the first one found.
/// This class only imports and validates the overlay in isolation; it does not merge it into a
/// <see cref="TechnicalCatalog"/>.
/// </remarks>
public static class SemanticOverlayImporter
{
    private static readonly MarkdownPipeline FrontMatterPipeline = new MarkdownPipelineBuilder().UseYamlFrontMatter().Build();

    private static readonly IDeserializer StrictDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly JsonSchema Schema = LoadSchema();

    /// <summary>
    /// Imports an overlay expressed as a single Markdown document whose leading YAML front matter
    /// block carries the overlay metadata; the remaining Markdown body is the opaque narrative.
    /// </summary>
    public static SemanticOverlayImportResult ImportMarkdownWithFrontMatter(string markdownWithFrontMatter, TechnicalCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(markdownWithFrontMatter);
        ArgumentNullException.ThrowIfNull(catalog);

        var document = Markdown.Parse(markdownWithFrontMatter, FrontMatterPipeline);
        var frontMatter = document.OfType<YamlFrontMatterBlock>().FirstOrDefault();
        if (frontMatter is null)
        {
            return SemanticOverlayImportResult.Failure(
            [
                new SemanticOverlayValidationError(
                    SemanticOverlayValidationErrorCodes.MissingFrontMatter,
                    "$",
                    "The Markdown document does not contain a YAML front matter block."),
            ]);
        }

        var yaml = frontMatter.Lines.ToString();
        var markdownBody = ExtractMarkdownBody(markdownWithFrontMatter);
        return Import(yaml, markdownBody, catalog);
    }

    /// <summary>
    /// Imports an overlay expressed as separate YAML metadata and Markdown narrative documents.
    /// </summary>
    public static SemanticOverlayImportResult ImportYamlAndMarkdown(string yaml, string markdown, TechnicalCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(catalog);

        return Import(yaml, markdown, catalog);
    }

    private static SemanticOverlayImportResult Import(string yaml, string markdown, TechnicalCatalog catalog)
    {
        var errors = new List<SemanticOverlayValidationError>();

        JsonNode? jsonNode;
        try
        {
            jsonNode = ParseYamlToJson(yaml);
        }
        catch (YamlException ex)
        {
            errors.Add(new SemanticOverlayValidationError(
                SemanticOverlayValidationErrorCodes.YamlSyntaxInvalid,
                "$",
                $"The YAML document could not be parsed: {ex.Message}"));
            return SemanticOverlayImportResult.Failure(errors);
        }

        var evaluation = Schema.Evaluate(jsonNode, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (!evaluation.IsValid)
        {
            errors.AddRange(ConvertSchemaErrors(evaluation));
        }

        SemanticOverlayDocumentDto? dto = null;
        try
        {
            dto = StrictDeserializer.Deserialize<SemanticOverlayDocumentDto>(yaml);
        }
        catch (YamlException ex)
        {
            errors.Add(new SemanticOverlayValidationError(
                SemanticOverlayValidationErrorCodes.StrictDeserializationFailed,
                "$",
                $"Strict typed YAML deserialization failed: {ex.Message}"));
        }

        if (dto is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.CatalogVersion))
            {
                errors.Add(new SemanticOverlayValidationError(
                    SemanticOverlayValidationErrorCodes.CatalogVersionRequired,
                    "$.catalogVersion",
                    "catalogVersion is required and cannot be empty."));
            }

            ValidatePhysicalReferences(dto, catalog, errors);
        }

        if (errors.Count > 0)
        {
            return SemanticOverlayImportResult.Failure(errors);
        }

        return SemanticOverlayImportResult.Success(BuildOverlay(dto!, markdown));
    }

    private static void ValidatePhysicalReferences(
        SemanticOverlayDocumentDto dto,
        TechnicalCatalog catalog,
        List<SemanticOverlayValidationError> errors)
    {
        if (dto.Entities is null)
        {
            return;
        }

        var catalogByIdentity = new Dictionary<PhysicalObjectIdentity, TechnicalEntity>();
        foreach (var entity in catalog.Entities)
        {
            catalogByIdentity[entity.Identity] = entity;
        }

        var seenSources = new HashSet<PhysicalObjectIdentity>();

        for (var entityIndex = 0; entityIndex < dto.Entities.Count; entityIndex++)
        {
            var entityDto = dto.Entities[entityIndex];
            var entityPath = string.Create(CultureInfo.InvariantCulture, $"$.entities[{entityIndex}]");

            if (string.IsNullOrWhiteSpace(entityDto.Source))
            {
                // Already reported as a required-field schema violation; nothing further to check here.
                continue;
            }

            var identity = ParsePhysicalReference(entityDto.Source);
            var physicalEntity = identity is not null && catalogByIdentity.TryGetValue(identity, out var foundEntity)
                ? foundEntity
                : null;

            if (physicalEntity is null)
            {
                errors.Add(new SemanticOverlayValidationError(
                    SemanticOverlayValidationErrorCodes.EntitySourceNotFound,
                    $"{entityPath}.source",
                    $"The entity source '{entityDto.Source}' does not resolve to a discovered physical table or view."));
            }
            else if (!seenSources.Add(identity!))
            {
                errors.Add(new SemanticOverlayValidationError(
                    SemanticOverlayValidationErrorCodes.EntitySourceDuplicate,
                    $"{entityPath}.source",
                    $"The entity source '{entityDto.Source}' is referenced by more than one overlay entity."));
            }

            var physicalFieldNames = physicalEntity is null
                ? null
                : new HashSet<string>(physicalEntity.Fields.Select(static field => field.Name), StringComparer.Ordinal);

            if (physicalFieldNames is not null && entityDto.Fields is not null)
            {
                foreach (var fieldKey in entityDto.Fields.Keys)
                {
                    if (!physicalFieldNames.Contains(fieldKey))
                    {
                        errors.Add(new SemanticOverlayValidationError(
                            SemanticOverlayValidationErrorCodes.FieldNotFound,
                            $"{entityPath}.fields.{fieldKey}",
                            $"The field '{fieldKey}' is not present on the physical entity '{entityDto.Source}'."));
                    }
                }
            }

            if (entityDto.Relationships is null)
            {
                continue;
            }

            foreach (var (relationshipName, relationshipDto) in entityDto.Relationships)
            {
                ValidateRelationship(entityPath, relationshipName, relationshipDto, entityDto.Source, physicalFieldNames, catalogByIdentity, errors);
            }
        }
    }

    private static void ValidateRelationship(
        string entityPath,
        string relationshipName,
        SemanticOverlayRelationshipDto relationshipDto,
        string entitySource,
        HashSet<string>? physicalFieldNames,
        Dictionary<PhysicalObjectIdentity, TechnicalEntity> catalogByIdentity,
        List<SemanticOverlayValidationError> errors)
    {
        var relationshipPath = $"{entityPath}.relationships.{relationshipName}";

        HashSet<string>? targetFieldNames = null;
        if (!string.IsNullOrWhiteSpace(relationshipDto.Target))
        {
            var targetIdentity = ParsePhysicalReference(relationshipDto.Target);
            var targetEntity = targetIdentity is not null && catalogByIdentity.TryGetValue(targetIdentity, out var foundTarget)
                ? foundTarget
                : null;

            if (targetEntity is null)
            {
                errors.Add(new SemanticOverlayValidationError(
                    SemanticOverlayValidationErrorCodes.RelationshipTargetNotFound,
                    $"{relationshipPath}.target",
                    $"The relationship target '{relationshipDto.Target}' does not resolve to a discovered physical table or view."));
            }
            else
            {
                targetFieldNames = new HashSet<string>(targetEntity.Fields.Select(static field => field.Name), StringComparer.Ordinal);
            }
        }

        if (relationshipDto.Join is null)
        {
            return;
        }

        for (var joinIndex = 0; joinIndex < relationshipDto.Join.Count; joinIndex++)
        {
            var pair = relationshipDto.Join[joinIndex];
            var pairPath = string.Create(CultureInfo.InvariantCulture, $"{relationshipPath}.join[{joinIndex}]");

            if (physicalFieldNames is not null
                && !string.IsNullOrWhiteSpace(pair.SourceField)
                && !physicalFieldNames.Contains(pair.SourceField))
            {
                errors.Add(new SemanticOverlayValidationError(
                    SemanticOverlayValidationErrorCodes.JoinSourceFieldNotFound,
                    $"{pairPath}.sourceField",
                    $"The join source field '{pair.SourceField}' is not present on the physical entity '{entitySource}'."));
            }

            if (targetFieldNames is not null
                && !string.IsNullOrWhiteSpace(pair.TargetField)
                && !targetFieldNames.Contains(pair.TargetField))
            {
                errors.Add(new SemanticOverlayValidationError(
                    SemanticOverlayValidationErrorCodes.JoinTargetFieldNotFound,
                    $"{pairPath}.targetField",
                    $"The join target field '{pair.TargetField}' is not present on the physical entity '{relationshipDto.Target}'."));
            }
        }
    }

    private static PhysicalObjectIdentity? ParsePhysicalReference(string reference)
    {
        var separatorIndex = reference.IndexOf('.', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex == reference.Length - 1)
        {
            return null;
        }

        var schema = reference[..separatorIndex];
        var objectName = reference[(separatorIndex + 1)..];
        return string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(objectName)
            ? null
            : new PhysicalObjectIdentity(schema, objectName);
    }

    private static SemanticOverlay BuildOverlay(SemanticOverlayDocumentDto dto, string markdown)
    {
        var entities = (dto.Entities ?? []).Select(BuildEntity);
        var warnings = (dto.Warnings ?? []).Select(static warning =>
            new SemanticOverlayWarning(warning.Title ?? string.Empty, warning.Content ?? string.Empty));

        return new SemanticOverlay(
            dto.CatalogVersion!,
            entities,
            dto.Name,
            dto.Title,
            dto.Description,
            warnings,
            markdown);
    }

    private static SemanticOverlayEntity BuildEntity(SemanticOverlayEntityDto entityDto)
    {
        var source = ParsePhysicalReference(entityDto.Source!)!;
        var fields = (entityDto.Fields ?? []).Select(static entry =>
            new KeyValuePair<string, SemanticOverlayField>(
                entry.Key,
                new SemanticOverlayField(entry.Value.Name, entry.Value.DisplayName, entry.Value.Description, entry.Value.Aliases)));
        var relationships = (entityDto.Relationships ?? []).Select(static entry =>
            new KeyValuePair<string, SemanticOverlayRelationship>(entry.Key, BuildRelationship(entry.Value)));
        var odata = entityDto.Odata is null
            ? null
            : new SemanticOverlayODataSettings(entityDto.Odata.Enabled, entityDto.Odata.EntitySetName, entityDto.Odata.Key);

        return new SemanticOverlayEntity(
            source,
            entityDto.Name,
            entityDto.DisplayName,
            entityDto.Description,
            entityDto.Aliases,
            entityDto.Exposed,
            odata,
            fields,
            relationships);
    }

    private static SemanticOverlayRelationship BuildRelationship(SemanticOverlayRelationshipDto relationshipDto)
    {
        var target = ParsePhysicalReference(relationshipDto.Target!)!;
        var join = (relationshipDto.Join ?? []).Select(static pair =>
            new SemanticOverlayJoinFieldPair(pair.SourceField!, pair.TargetField!));

        return new SemanticOverlayRelationship(target, join, ParseCardinality(relationshipDto.Cardinality), relationshipDto.Description);
    }

    private static SemanticOverlayCardinality? ParseCardinality(string? value) => value switch
    {
        null => null,
        "one-to-one" => SemanticOverlayCardinality.OneToOne,
        "one-to-many" => SemanticOverlayCardinality.OneToMany,
        "many-to-one" => SemanticOverlayCardinality.ManyToOne,
        "many-to-many" => SemanticOverlayCardinality.ManyToMany,
        _ => throw new InvalidOperationException(
            $"Unexpected cardinality value '{value}' reached overlay construction after passing schema validation."),
    };

    private static string ExtractMarkdownBody(string markdownWithFrontMatter)
    {
        using var reader = new StringReader(markdownWithFrontMatter);

        // The opening front-matter delimiter is already confirmed present (Markdig located the block);
        // discard it and every line up to and including the closing delimiter, then return the rest.
        _ = reader.ReadLine();

        var bodyLines = new List<string>();
        var closedFrontMatter = false;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!closedFrontMatter)
            {
                var trimmed = line.TrimEnd();
                if (trimmed is "---" or "...")
                {
                    closedFrontMatter = true;
                }

                continue;
            }

            bodyLines.Add(line);
        }

        while (bodyLines.Count > 0 && bodyLines[0].Length == 0)
        {
            bodyLines.RemoveAt(0);
        }

        return string.Join('\n', bodyLines);
    }

    private static JsonNode? ParseYamlToJson(string yaml)
    {
        using var reader = new StringReader(yaml);
        var stream = new YamlStream();
        stream.Load(reader);
        return stream.Documents.Count == 0 ? new JsonObject() : ConvertYamlNode(stream.Documents[0].RootNode);
    }

    private static JsonNode? ConvertYamlNode(YamlNode node) => node switch
    {
        YamlScalarNode scalar => ConvertScalar(scalar),
        YamlSequenceNode sequence => ConvertSequence(sequence),
        YamlMappingNode mapping => ConvertMapping(mapping),
        _ => throw new NotSupportedException($"Unsupported YAML node type '{node.GetType().Name}'."),
    };

    private static JsonArray ConvertSequence(YamlSequenceNode sequence)
    {
        var array = new JsonArray();
        foreach (var child in sequence.Children)
        {
            array.Add(ConvertYamlNode(child));
        }

        return array;
    }

    private static JsonObject ConvertMapping(YamlMappingNode mapping)
    {
        var obj = new JsonObject();
        foreach (var entry in mapping.Children)
        {
            var key = entry.Key is YamlScalarNode keyScalar ? keyScalar.Value ?? string.Empty : entry.Key.ToString();
            obj[key] = ConvertYamlNode(entry.Value);
        }

        return obj;
    }

    private static JsonValue? ConvertScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value;
        if (value is null)
        {
            return null;
        }

        if (scalar.Style == ScalarStyle.Plain)
        {
            if (value.Length == 0 || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (bool.TryParse(value, out var boolValue))
            {
                return JsonValue.Create(boolValue);
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            {
                return JsonValue.Create(longValue);
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
            {
                return JsonValue.Create(doubleValue);
            }
        }

        return JsonValue.Create(value);
    }

    private static IEnumerable<SemanticOverlayValidationError> ConvertSchemaErrors(EvaluationResults evaluation)
    {
        foreach (var detail in evaluation.Details)
        {
            if (!detail.HasErrors)
            {
                continue;
            }

            var pointer = detail.InstanceLocation.ToString();
            var path = pointer.Length == 0 ? "$" : "$" + pointer.Replace('/', '.');
            foreach (var error in detail.Errors!)
            {
                var message = string.IsNullOrEmpty(error.Key) ? error.Value : $"{error.Key}: {error.Value}";
                yield return new SemanticOverlayValidationError(SemanticOverlayValidationErrorCodes.SchemaViolation, path, message);
            }
        }
    }

    private static JsonSchema LoadSchema()
    {
        var assembly = typeof(SemanticOverlayImporter).Assembly;
        using var stream = assembly.GetManifestResourceStream("TheSqlODataMcp.Core.Catalog.Schemas.semantic-overlay.v1.schema.json")
            ?? throw new InvalidOperationException("The embedded semantic overlay schema resource was not found.");
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }
}
