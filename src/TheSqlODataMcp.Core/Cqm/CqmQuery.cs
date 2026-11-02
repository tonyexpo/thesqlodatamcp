using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace TheSqlODataMcp.Core.Cqm;

/// <summary>
/// The versioned, provider-neutral representation of one read query. This is the v1 contract shared by
/// MCP, OData, JSON, and HTTP <c>QUERY</c>; protocol parsers translate into it before catalog resolution,
/// validation, or execution. The public model cannot represent writes, raw SQL, or SQL fragments.
/// </summary>
public sealed class CqmQuery
{
    private readonly IReadOnlyList<CqmJoin> joins;
    private readonly IReadOnlyList<CqmSelectItem> select;
    private readonly IReadOnlyList<CqmExpression> groupBy;
    private readonly IReadOnlyList<CqmOrderBy> orderBy;

    public CqmQuery(
        CqmEntitySource from,
        CqmPage page,
        IReadOnlyList<CqmJoin>? joins = null,
        IReadOnlyList<CqmSelectItem>? select = null,
        CqmExpression? where = null,
        IReadOnlyList<CqmExpression>? groupBy = null,
        CqmExpression? having = null,
        IReadOnlyList<CqmOrderBy>? orderBy = null,
        bool distinct = false,
        bool includeTotalCount = false,
        string version = CqmVersions.V1)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(page);

        Version = CqmVersions.RequireSupported(version);
        From = from;
        Page = page;
        this.joins = CqmCollections.Copy(joins, nameof(joins));
        this.select = CqmCollections.Copy(select, nameof(select));
        Where = where;
        this.groupBy = CqmCollections.Copy(groupBy, nameof(groupBy));
        Having = having;
        this.orderBy = CqmCollections.Copy(orderBy, nameof(orderBy));
        Distinct = distinct;
        IncludeTotalCount = includeTotalCount;
    }

    /// <summary>The exact contract version, currently always <c>1.0</c>.</summary>
    public string Version { get; }

    /// <summary>The single source entity every query begins from.</summary>
    public CqmEntitySource From { get; }

    /// <summary>Optional ordered joins. Join priority and catalog resolution are validated by later CQM work.</summary>
    public IReadOnlyList<CqmJoin> Joins => joins;

    /// <summary>Selected expressions with stable output aliases. An empty list is allowed for now.</summary>
    public IReadOnlyList<CqmSelectItem> Select => select;

    /// <summary>Optional row filter. It cannot contain aggregates.</summary>
    public CqmExpression? Where { get; }

    /// <summary>Grouping expressions. Presence classifies the query as analytical.</summary>
    public IReadOnlyList<CqmExpression> GroupBy => groupBy;

    /// <summary>Optional post-aggregation filter.</summary>
    public CqmExpression? Having { get; }

    /// <summary>Ordered sort keys.</summary>
    public IReadOnlyList<CqmOrderBy> OrderBy => orderBy;

    /// <summary>Whether duplicate result rows are suppressed.</summary>
    public bool Distinct { get; }

    /// <summary>The explicit offset page. The contract always requires bounded paging.</summary>
    public CqmPage Page { get; }

    /// <summary>Whether the caller requests a total row count alongside the page.</summary>
    public bool IncludeTotalCount { get; }
}

/// <summary>Supported CQM contract versions.</summary>
public static class CqmVersions
{
    /// <summary>The only supported version in this slice.</summary>
    public const string V1 = "1.0";

    private static readonly IReadOnlyList<string> SupportedVersions = new ReadOnlyCollection<string>([V1]);

    public static IReadOnlyList<string> Supported => SupportedVersions;

    public static string RequireSupported(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version, nameof(version));
        if (!string.Equals(version, V1, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The CQM version '{version}' is not supported.", nameof(version));
        }

        return version;
    }
}

/// <summary>One physical entity source and its query-local alias.</summary>
public sealed class CqmEntitySource
{
    public CqmEntitySource(string entity, string alias)
    {
        Entity = CqmNames.RequireEntity(entity, nameof(entity));
        Alias = CqmNames.RequireAlias(alias, nameof(alias));
    }

    /// <summary>The catalog entity reference, in <c>schema.object</c> form.</summary>
    public string Entity { get; }

    /// <summary>A safe query-local alias used by field references.</summary>
    public string Alias { get; }
}

public enum CqmJoinType
{
    Inner,
    Left,
}

/// <summary>
/// One join. At most one of <see cref="On"/> and <see cref="Relationship"/> is present: an explicit
/// condition always wins, a named relationship defers resolution to the catalog, and neither asks the
/// resolver to use the one unambiguous discovered/configured relationship for the pair.
/// </summary>
public sealed class CqmJoin
{
    public CqmJoin(CqmJoinType type, CqmEntitySource source, CqmExpression? on = null, string? relationship = null)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown join type.");
        }

        ArgumentNullException.ThrowIfNull(source);
        if (on is not null && relationship is not null)
        {
            throw new ArgumentException("on and relationship are mutually exclusive; omit both for automatic resolution.", nameof(on));
        }

        Type = type;
        Source = source;
        On = on;
        Relationship = relationship is null ? null : CqmNames.RequireRelationship(relationship, nameof(relationship));
    }

    public CqmJoinType Type { get; }

    public CqmEntitySource Source { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CqmExpression? On { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Relationship { get; }
}

/// <summary>One selected expression and its stable output alias.</summary>
public sealed class CqmSelectItem
{
    public CqmSelectItem(CqmExpression expression, string alias)
    {
        ArgumentNullException.ThrowIfNull(expression);
        Expression = expression;
        Alias = CqmNames.RequireAlias(alias, nameof(alias));
    }

    public CqmExpression Expression { get; }

    public string Alias { get; }
}

public enum CqmSortDirection
{
    Asc,
    Desc,
}

/// <summary>One ordered sort key.</summary>
public sealed class CqmOrderBy
{
    public CqmOrderBy(CqmExpression expression, CqmSortDirection direction)
    {
        ArgumentNullException.ThrowIfNull(expression);
        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown sort direction.");
        }

        Expression = expression;
        Direction = direction;
    }

    public CqmExpression Expression { get; }

    public CqmSortDirection Direction { get; }
}

/// <summary>An explicit, bounded offset page.</summary>
public sealed class CqmPage
{
    public CqmPage(int limit, int offset)
    {
        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "The page limit must be at least one.");
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "The page offset cannot be negative.");
        }

        Limit = limit;
        Offset = offset;
    }

    public int Limit { get; }

    public int Offset { get; }
}

internal static class CqmCollections
{
    public static IReadOnlyList<T> Copy<T>(IReadOnlyList<T>? values, string parameterName)
    {
        if (values is null)
        {
            return Array.Empty<T>();
        }

        var copy = values.ToArray();
        if (copy.Any(static value => value is null))
        {
            throw new ArgumentException("Collections cannot contain null values.", parameterName);
        }

        return new ReadOnlyCollection<T>(copy);
    }
}

internal static class CqmNames
{
    private const int MaxAliasLength = 128;
    private const int MaxReferenceLength = 512;

    public static string RequireAlias(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > MaxAliasLength)
        {
            throw new ArgumentException($"An alias cannot exceed {MaxAliasLength} characters.", parameterName);
        }

        if (!(char.IsAsciiLetter(value[0]) || value[0] == '_'))
        {
            throw new ArgumentException("An alias must start with an ASCII letter or underscore.", parameterName);
        }

        foreach (var character in value)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character == '_'))
            {
                throw new ArgumentException("An alias may contain only ASCII letters, digits, and underscores.", parameterName);
            }
        }

        return value;
    }

    public static string RequireEntity(string value, string parameterName) => RequireReference(value, parameterName, "entity");

    public static string RequireFieldReference(string value, string parameterName) => RequireReference(value, parameterName, "field reference");

    public static string RequireRelationship(string value, string parameterName) => RequireReference(value, parameterName, "relationship name");

    private static string RequireReference(string value, string parameterName, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!CqmUnicode.IsValidUtf16(value))
        {
            throw new ArgumentException($"A {description} must contain valid UTF-16 text.", parameterName);
        }

        if (value.Length > MaxReferenceLength)
        {
            throw new ArgumentException($"A {description} cannot exceed {MaxReferenceLength} characters.", parameterName);
        }

        return value;
    }
}
