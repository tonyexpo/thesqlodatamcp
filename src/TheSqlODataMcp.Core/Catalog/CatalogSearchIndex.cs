namespace TheSqlODataMcp.Core.Catalog;

/// <summary>
/// An in-memory index over one <see cref="MergedCatalog"/> snapshot: O(1) lookup by physical identity
/// (<see cref="FindEntity"/>), plus a simple ranked <see cref="Search"/> across entity/field names,
/// aliases, descriptions, catalog-level warnings, and relationships — exactly the surface the handoff's
/// <c>search_catalog</c> tool asks to search (<c>docs/AI_DATA_GATEWAY_HANDOFF.md</c>, "MCP tools"). This
/// is only the data structure that tool will sit on top of: its JSON schema, pagination, and ranking
/// tuned against real usage are later Milestone 4 work, not part of this Milestone 1 slice.
/// </summary>
public sealed class CatalogSearchIndex
{
    private readonly IReadOnlyDictionary<PhysicalObjectIdentity, MergedEntity> entitiesByIdentity;

    public CatalogSearchIndex(MergedCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Catalog = catalog;
        entitiesByIdentity = catalog.Entities.ToDictionary(entity => entity.Physical.Identity);
    }

    public MergedCatalog Catalog { get; }

    /// <summary>The entity at <paramref name="identity"/>, or null if the catalog has no such entity.</summary>
    public MergedEntity? FindEntity(PhysicalObjectIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return entitiesByIdentity.GetValueOrDefault(identity);
    }

    /// <summary>
    /// Ranked matches for <paramref name="query"/>, most relevant first, capped at
    /// <paramref name="maxResults"/>. Empty for a null/whitespace query. A name-like match (entity/field
    /// name or display name, an alias, a relationship name) always outranks a free-text match
    /// (a description or warning) at the same exactness; ties break deterministically by entity
    /// schema/name, then match kind, then matched text.
    /// </summary>
    /// <remarks>
    /// Computed by scanning the catalog on every call rather than through a precomputed inverted index:
    /// nothing consumes this yet and a single reporting catalog's entity count is small (see
    /// <c>docs/architecture.md</c>'s single-instance v1 constraint), so the extra structure would be
    /// speculative. Revisit if a real caller makes this measurably hot.
    /// </remarks>
    public IReadOnlyList<CatalogSearchMatch> Search(string query, int maxResults = 20)
    {
        if (maxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults), "At least one result must be requested.");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var trimmedQuery = query.Trim();
        var matches = new List<CatalogSearchMatch>();
        var seen = new HashSet<(MergedEntity? Entity, MergedField? Field, MergedRelationship? Relationship, CatalogSearchMatchKind Kind, string Text)>();

        foreach (var warning in Catalog.Warnings)
        {
            TryAddMatch(matches, seen, entity: null, CatalogSearchMatchKind.Warning, warning.Title, trimmedQuery, isName: false);
            TryAddMatch(matches, seen, entity: null, CatalogSearchMatchKind.Warning, warning.Content, trimmedQuery, isName: false);
        }

        foreach (var entity in Catalog.Entities)
        {
            TryAddMatch(matches, seen, entity, CatalogSearchMatchKind.EntityName, entity.Physical.Identity.ObjectName, trimmedQuery, isName: true);
            TryAddMatch(matches, seen, entity, CatalogSearchMatchKind.EntityName, entity.DisplayName, trimmedQuery, isName: true);
            TryAddMatch(matches, seen, entity, CatalogSearchMatchKind.EntityDescription, entity.Description, trimmedQuery, isName: false);
            foreach (var alias in entity.Aliases)
            {
                TryAddMatch(matches, seen, entity, CatalogSearchMatchKind.EntityAlias, alias, trimmedQuery, isName: true);
            }

            foreach (var field in entity.Fields)
            {
                TryAddMatch(matches, seen, entity, CatalogSearchMatchKind.FieldName, field.Physical.Name, trimmedQuery, isName: true, field: field);
                TryAddMatch(matches, seen, entity, CatalogSearchMatchKind.FieldName, field.DisplayName, trimmedQuery, isName: true, field: field);
                TryAddMatch(matches, seen, entity, CatalogSearchMatchKind.FieldDescription, field.Description, trimmedQuery, isName: false, field: field);
                foreach (var alias in field.Aliases)
                {
                    TryAddMatch(matches, seen, entity, CatalogSearchMatchKind.FieldAlias, alias, trimmedQuery, isName: true, field: field);
                }
            }

            foreach (var relationship in entity.Relationships)
            {
                TryAddMatch(matches, seen, entity, CatalogSearchMatchKind.Relationship, relationship.Name, trimmedQuery, isName: true, relationship: relationship);
                TryAddMatch(matches, seen, entity, CatalogSearchMatchKind.Relationship, relationship.Description, trimmedQuery, isName: false, relationship: relationship);
            }
        }

        return matches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Entity?.Physical.Identity.Schema, StringComparer.Ordinal)
            .ThenBy(match => match.Entity?.Physical.Identity.ObjectName, StringComparer.Ordinal)
            .ThenBy(match => match.Kind)
            .ThenBy(match => match.MatchedText, StringComparer.Ordinal)
            .Take(maxResults)
            .ToArray();
    }

    private static void TryAddMatch(
        List<CatalogSearchMatch> matches,
        HashSet<(MergedEntity? Entity, MergedField? Field, MergedRelationship? Relationship, CatalogSearchMatchKind Kind, string Text)> seen,
        MergedEntity? entity,
        CatalogSearchMatchKind kind,
        string? text,
        string query,
        bool isName,
        MergedField? field = null,
        MergedRelationship? relationship = null)
    {
        if (string.IsNullOrEmpty(text) || !seen.Add((entity, field, relationship, kind, text)))
        {
            return;
        }

        var score = ScoreMatch(text, query, isName);
        if (score > 0)
        {
            matches.Add(new CatalogSearchMatch(entity, kind, text, score, field, relationship));
        }
    }

    private static int ScoreMatch(string text, string query, bool isName)
    {
        var index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return 0;
        }

        if (string.Equals(text, query, StringComparison.OrdinalIgnoreCase))
        {
            return isName ? 100 : 40;
        }

        if (index == 0)
        {
            return isName ? 60 : 20;
        }

        return isName ? 30 : 10;
    }
}

/// <summary>What in the merged catalog a <see cref="CatalogSearchMatch"/> was found in.</summary>
public enum CatalogSearchMatchKind
{
    EntityName,
    EntityAlias,
    EntityDescription,
    FieldName,
    FieldAlias,
    FieldDescription,
    Relationship,
    Warning,
}

/// <summary>
/// One ranked hit from <see cref="CatalogSearchIndex.Search"/>. <see cref="Field"/>/<see cref="Relationship"/>
/// disambiguate which specific field or relationship matched when two of them on the same entity share
/// identical match text (e.g. the same display name) — without them, two genuinely distinct matches would
/// be indistinguishable to a caller, and <see cref="CatalogSearchIndex"/>'s own duplicate-match guard would
/// have no way to tell "the same match seen twice" apart from "two different fields with the same label."
/// </summary>
public sealed class CatalogSearchMatch
{
    public CatalogSearchMatch(
        MergedEntity? entity,
        CatalogSearchMatchKind kind,
        string matchedText,
        int score,
        MergedField? field = null,
        MergedRelationship? relationship = null)
    {
        if (entity is null && (field is not null || relationship is not null))
        {
            throw new ArgumentException("A field or relationship match must belong to an entity.", nameof(entity));
        }

        Entity = entity;
        Field = field;
        Relationship = relationship;
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "The catalog search match kind is not supported.");
        }

        Kind = kind;
        MatchedText = TechnicalCatalog.RequireIdentifier(matchedText, nameof(matchedText));
        if (score <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "A match must have a positive score.");
        }

        Score = score;
    }

    /// <summary>The entity this match belongs to, or null for a catalog-level warning match.</summary>
    public MergedEntity? Entity { get; }

    /// <summary>The specific field this match belongs to, for <see cref="CatalogSearchMatchKind.FieldName"/>/<see cref="CatalogSearchMatchKind.FieldAlias"/>/<see cref="CatalogSearchMatchKind.FieldDescription"/>; otherwise null.</summary>
    public MergedField? Field { get; }

    /// <summary>The specific relationship this match belongs to, for <see cref="CatalogSearchMatchKind.Relationship"/>; otherwise null.</summary>
    public MergedRelationship? Relationship { get; }

    public CatalogSearchMatchKind Kind { get; }

    /// <summary>The exact text (a name, alias, description, or warning) that matched the query.</summary>
    public string MatchedText { get; }

    public int Score { get; }
}
