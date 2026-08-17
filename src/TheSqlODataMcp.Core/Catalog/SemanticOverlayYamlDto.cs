namespace TheSqlODataMcp.Core.Catalog;

// Untyped, mutable data-transfer shapes used only as the target of strict, camel-case YAML
// deserialization (see SemanticOverlayImporter). Every property is nullable-by-default so a
// partially populated document can still be deserialized for validation; required-ness and
// structural rules are enforced separately by the versioned JSON Schema and by
// SemanticOverlayImporter's physical-reference validation, not by these DTOs.
//
// Deliberately not calling the deserializer's IgnoreUnmatchedProperties(): any YAML key that has no
// matching property here (including the six forbidden top-level sections) must make YamlDotNet throw,
// which SemanticOverlayImporter surfaces as a validation error.

internal sealed class SemanticOverlayDocumentDto
{
    public string? CatalogVersion { get; set; }

    public string? Name { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public List<SemanticOverlayEntityDto>? Entities { get; set; }

    public List<SemanticOverlayWarningDto>? Warnings { get; set; }
}

internal sealed class SemanticOverlayEntityDto
{
    public string? Source { get; set; }

    public string? Name { get; set; }

    public string? DisplayName { get; set; }

    public string? Description { get; set; }

    public List<string>? Aliases { get; set; }

    public bool? Exposed { get; set; }

    public SemanticOverlayODataDto? Odata { get; set; }

    public Dictionary<string, SemanticOverlayFieldDto>? Fields { get; set; }

    public Dictionary<string, SemanticOverlayRelationshipDto>? Relationships { get; set; }
}

internal sealed class SemanticOverlayODataDto
{
    public bool? Enabled { get; set; }

    public string? EntitySetName { get; set; }

    public List<string>? Key { get; set; }
}

internal sealed class SemanticOverlayFieldDto
{
    public string? Name { get; set; }

    public string? DisplayName { get; set; }

    public string? Description { get; set; }

    public List<string>? Aliases { get; set; }
}

internal sealed class SemanticOverlayRelationshipDto
{
    public string? Target { get; set; }

    public string? Cardinality { get; set; }

    public string? Description { get; set; }

    public List<SemanticOverlayJoinPairDto>? Join { get; set; }
}

internal sealed class SemanticOverlayJoinPairDto
{
    public string? SourceField { get; set; }

    public string? TargetField { get; set; }
}

internal sealed class SemanticOverlayWarningDto
{
    public string? Title { get; set; }

    public string? Content { get; set; }
}
