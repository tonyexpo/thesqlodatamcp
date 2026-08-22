using System.Globalization;

namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// Merges a <see cref="TechnicalCatalog"/> with an optional <see cref="SemanticOverlay"/> into a
/// <see cref="Catalog.MergedCatalog"/>. When an overlay is supplied, its physical references are
/// re-validated against the specific <see cref="TechnicalCatalog"/> passed to this call — never assumed
/// to be the same instance the overlay was originally imported against — collecting every independently
/// detectable problem in one pass, matching <see cref="SemanticOverlayImportResult"/>'s design. Merging
/// with no overlay always succeeds, per the handoff's "<c>configured: false</c> when absent" behavior.
/// </summary>
public static class CatalogMerger
{
    public static CatalogMergeResult Merge(TechnicalCatalog catalog, SemanticOverlay? overlay)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var catalogByIdentity = catalog.Entities.ToDictionary(static entity => entity.Identity);

        if (overlay is null)
        {
            return CatalogMergeResult.Success(BuildCatalog(catalog, overlay: null, overlayByIdentity: null));
        }

        var errors = new List<SemanticOverlayValidationError>();

        if (!string.Equals(overlay.CatalogVersion, catalog.CatalogVersion, StringComparison.Ordinal))
        {
            errors.Add(new SemanticOverlayValidationError(
                CatalogMergeErrorCodes.CatalogVersionMismatch,
                "$.catalogVersion",
                $"The overlay catalog version '{overlay.CatalogVersion}' does not match the technical catalog version '{catalog.CatalogVersion}'."));
        }

        var overlayByIdentity = new Dictionary<PhysicalObjectIdentity, SemanticOverlayEntity>();

        for (var entityIndex = 0; entityIndex < overlay.Entities.Count; entityIndex++)
        {
            var overlayEntity = overlay.Entities[entityIndex];
            var entityPath = string.Create(CultureInfo.InvariantCulture, $"$.entities[{entityIndex}]");

            if (!catalogByIdentity.TryGetValue(overlayEntity.Source, out var physicalEntity))
            {
                errors.Add(new SemanticOverlayValidationError(
                    CatalogMergeErrorCodes.EntitySourceNotFound,
                    $"{entityPath}.source",
                    $"The entity source '{overlayEntity.Source}' does not resolve to a physical table or view in the supplied catalog."));
                continue;
            }

            overlayByIdentity[overlayEntity.Source] = overlayEntity;

            var physicalFieldNames = new HashSet<string>(physicalEntity.Fields.Select(static field => field.Name), StringComparer.Ordinal);

            foreach (var fieldKey in overlayEntity.Fields.Keys)
            {
                if (!physicalFieldNames.Contains(fieldKey))
                {
                    errors.Add(new SemanticOverlayValidationError(
                        CatalogMergeErrorCodes.FieldNotFound,
                        $"{entityPath}.fields.{fieldKey}",
                        $"The field '{fieldKey}' is not present on the physical entity '{overlayEntity.Source}'."));
                }
            }

            foreach (var (relationshipName, relationship) in overlayEntity.Relationships)
            {
                if (string.IsNullOrWhiteSpace(relationshipName))
                {
                    errors.Add(new SemanticOverlayValidationError(
                        CatalogMergeErrorCodes.RelationshipNameInvalid,
                        $"{entityPath}.relationships",
                        "A relationship name cannot be empty or whitespace-only."));
                    continue;
                }

                ValidateRelationship(entityPath, relationshipName, relationship, overlayEntity.Source, physicalFieldNames, catalogByIdentity, errors);
            }

            if (overlayEntity.OData is not null)
            {
                var seenKeyFields = new HashSet<string>(StringComparer.Ordinal);
                for (var keyIndex = 0; keyIndex < overlayEntity.OData.Key.Count; keyIndex++)
                {
                    var keyField = overlayEntity.OData.Key[keyIndex];
                    var keyFieldPath = string.Create(CultureInfo.InvariantCulture, $"{entityPath}.odata.key[{keyIndex}]");

                    if (!seenKeyFields.Add(keyField))
                    {
                        errors.Add(new SemanticOverlayValidationError(
                            CatalogMergeErrorCodes.ODataKeyFieldDuplicate,
                            keyFieldPath,
                            $"The OData key field '{keyField}' is repeated more than once in the odata.key list for '{overlayEntity.Source}'."));
                    }

                    if (!physicalFieldNames.Contains(keyField))
                    {
                        errors.Add(new SemanticOverlayValidationError(
                            CatalogMergeErrorCodes.ODataKeyFieldNotFound,
                            keyFieldPath,
                            $"The OData key field '{keyField}' is not present on the physical entity '{overlayEntity.Source}'."));
                    }
                }
            }
        }

        if (errors.Count > 0)
        {
            return CatalogMergeResult.Failure(errors);
        }

        return CatalogMergeResult.Success(BuildCatalog(catalog, overlay, overlayByIdentity));
    }

    private static void ValidateRelationship(
        string entityPath,
        string relationshipName,
        SemanticOverlayRelationship relationship,
        PhysicalObjectIdentity entitySource,
        HashSet<string> physicalFieldNames,
        Dictionary<PhysicalObjectIdentity, TechnicalEntity> catalogByIdentity,
        List<SemanticOverlayValidationError> errors)
    {
        var relationshipPath = $"{entityPath}.relationships.{relationshipName}";

        HashSet<string>? targetFieldNames = null;
        if (!catalogByIdentity.TryGetValue(relationship.Target, out var targetEntity))
        {
            errors.Add(new SemanticOverlayValidationError(
                CatalogMergeErrorCodes.RelationshipTargetNotFound,
                $"{relationshipPath}.target",
                $"The relationship target '{relationship.Target}' does not resolve to a physical table or view in the supplied catalog."));
        }
        else
        {
            targetFieldNames = new HashSet<string>(targetEntity.Fields.Select(static field => field.Name), StringComparer.Ordinal);
        }

        for (var joinIndex = 0; joinIndex < relationship.Join.Count; joinIndex++)
        {
            var pair = relationship.Join[joinIndex];
            var pairPath = string.Create(CultureInfo.InvariantCulture, $"{relationshipPath}.join[{joinIndex}]");

            if (!physicalFieldNames.Contains(pair.SourceField))
            {
                errors.Add(new SemanticOverlayValidationError(
                    CatalogMergeErrorCodes.JoinSourceFieldNotFound,
                    $"{pairPath}.sourceField",
                    $"The join source field '{pair.SourceField}' is not present on the physical entity '{entitySource}'."));
            }

            if (targetFieldNames is not null && !targetFieldNames.Contains(pair.TargetField))
            {
                errors.Add(new SemanticOverlayValidationError(
                    CatalogMergeErrorCodes.JoinTargetFieldNotFound,
                    $"{pairPath}.targetField",
                    $"The join target field '{pair.TargetField}' is not present on the physical entity '{relationship.Target}'."));
            }
        }
    }

    private static MergedCatalog BuildCatalog(
        TechnicalCatalog catalog,
        SemanticOverlay? overlay,
        Dictionary<PhysicalObjectIdentity, SemanticOverlayEntity>? overlayByIdentity)
    {
        var entities = catalog.Entities.Select(physicalEntity =>
        {
            SemanticOverlayEntity? overlayEntity = null;
            overlayByIdentity?.TryGetValue(physicalEntity.Identity, out overlayEntity);
            return BuildEntity(physicalEntity, overlayEntity);
        });

        return new MergedCatalog(
            catalog.CatalogVersion,
            catalog.Provider,
            configured: overlay is not null,
            entities,
            overlay?.Name,
            overlay?.Title,
            overlay?.Description,
            overlay?.Warnings,
            overlay?.Markdown);
    }

    private static MergedEntity BuildEntity(TechnicalEntity physicalEntity, SemanticOverlayEntity? overlayEntity)
    {
        var displayName =
            !string.IsNullOrWhiteSpace(overlayEntity?.DisplayName) ? overlayEntity.DisplayName
            : !string.IsNullOrWhiteSpace(overlayEntity?.Name) ? overlayEntity!.Name
            : physicalEntity.Identity.ObjectName;

        var description = overlayEntity?.Description ?? physicalEntity.Description;
        var exposed = overlayEntity?.Exposed ?? true;
        var odata = overlayEntity?.OData;

        var fields = physicalEntity.Fields.Select(physicalField =>
        {
            SemanticOverlayField? overlayField = null;
            overlayEntity?.Fields.TryGetValue(physicalField.Name, out overlayField);
            return new MergedField(
                physicalField,
                overlayField?.DisplayName,
                overlayField?.Description ?? physicalField.Description,
                overlayField?.Aliases);
        });

        return new MergedEntity(
            physicalEntity,
            displayName!,
            fields,
            overlayEntity?.Aliases,
            description,
            exposed,
            odata,
            ResolveEffectiveKeyFields(physicalEntity, odata),
            BuildRelationships(physicalEntity, overlayEntity));
    }

    private static IReadOnlyList<string> ResolveEffectiveKeyFields(TechnicalEntity physicalEntity, SemanticOverlayODataSettings? odata)
    {
        if (odata is not null && odata.Key.Count > 0)
        {
            return odata.Key;
        }

        var primaryKey = physicalEntity.Keys.SingleOrDefault(static key => key.IsPrimary);
        return primaryKey?.Fields ?? Array.Empty<string>();
    }

    private static IEnumerable<MergedRelationship> BuildRelationships(TechnicalEntity physicalEntity, SemanticOverlayEntity? overlayEntity)
    {
        foreach (var discovered in physicalEntity.Relationships)
        {
            yield return new MergedRelationship(
                discovered.Name,
                discovered.Target,
                discovered.FieldPairs,
                RelationshipProvenance.Discovered,
                cardinality: null,
                discovered.Description);
        }

        if (overlayEntity is null)
        {
            yield break;
        }

        foreach (var (name, configured) in overlayEntity.Relationships)
        {
            yield return new MergedRelationship(
                name,
                configured.Target,
                configured.Join.Select(static pair => new RelationshipFieldPair(pair.SourceField, pair.TargetField)),
                RelationshipProvenance.Configured,
                configured.Cardinality,
                configured.Description);
        }
    }
}
