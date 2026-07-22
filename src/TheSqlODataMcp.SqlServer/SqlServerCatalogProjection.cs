using TheSqlODataMcp.Core.Catalog;

namespace TheSqlODataMcp.SqlServer;

internal sealed record SqlServerCatalogColumnMetadata(
    int ObjectId,
    string SchemaName,
    string ObjectName,
    string ObjectType,
    int TemporalType,
    string ColumnName,
    int ColumnId,
    bool IsNullable,
    bool IsIdentity,
    bool IsComputed,
    bool IsPersistedComputed,
    int GeneratedAlwaysType,
    int SystemTypeId,
    string ProviderTypeName,
    int MaxLength,
    int Precision,
    int Scale,
    string? ObjectDescription,
    string? ColumnDescription);

internal static class SqlServerCatalogProjection
{
    internal static TechnicalCatalog CreateCatalog(IEnumerable<SqlServerCatalogColumnMetadata> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var entities = rows
            .Select(ValidateRow)
            .GroupBy(static row => row.ObjectId)
            .Select(CreateEntity)
            .OrderBy(static entity => entity.Identity.Schema, StringComparer.Ordinal)
            .ThenBy(static entity => entity.Identity.ObjectName, StringComparer.Ordinal)
            .ToArray();

        return new TechnicalCatalog("1.0", "sqlserver", entities);
    }

    private static SqlServerCatalogColumnMetadata ValidateRow(SqlServerCatalogColumnMetadata row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.ObjectId <= 0)
        {
            throw new InvalidOperationException("SQL Server object identifiers must be positive.");
        }

        if (row.ColumnId <= 0)
        {
            throw new InvalidOperationException("SQL Server column identifiers must be positive.");
        }

        if (row.ObjectType is not ("U" or "V"))
        {
            throw new InvalidOperationException("Only SQL Server user tables and views can be projected.");
        }

        if (row.TemporalType is not (0 or 2))
        {
            throw new InvalidOperationException("SQL Server temporal metadata is inconsistent with catalog discovery.");
        }

        if (row.GeneratedAlwaysType is < 0 or > 2)
        {
            throw new InvalidOperationException("SQL Server generated-always metadata is not supported by this catalog slice.");
        }

        if (row.IsPersistedComputed && !row.IsComputed)
        {
            throw new InvalidOperationException("A persisted SQL Server computed column must be computed.");
        }

        return row;
    }

    private static TechnicalEntity CreateEntity(IGrouping<int, SqlServerCatalogColumnMetadata> objectRows)
    {
        var first = objectRows.First();
        foreach (var row in objectRows)
        {
            if (!HasSameObjectMetadata(first, row))
            {
                throw new InvalidOperationException("SQL Server metadata rows for one object are inconsistent.");
            }
        }

        var fields = objectRows
            .Select(CreateField)
            .OrderBy(static field => field.Ordinal)
            .ToArray();

        return new TechnicalEntity(
            new PhysicalObjectIdentity(first.SchemaName, first.ObjectName),
            first.ObjectType == "U" ? CatalogObjectKind.Table : CatalogObjectKind.View,
            fields,
            description: first.ObjectDescription,
            isTemporal: first.TemporalType == 2);
    }

    private static bool HasSameObjectMetadata(
        SqlServerCatalogColumnMetadata expected,
        SqlServerCatalogColumnMetadata actual) =>
        string.Equals(expected.SchemaName, actual.SchemaName, StringComparison.Ordinal)
        && string.Equals(expected.ObjectName, actual.ObjectName, StringComparison.Ordinal)
        && string.Equals(expected.ObjectType, actual.ObjectType, StringComparison.Ordinal)
        && expected.TemporalType == actual.TemporalType
        && string.Equals(expected.ObjectDescription, actual.ObjectDescription, StringComparison.Ordinal);

    private static TechnicalField CreateField(SqlServerCatalogColumnMetadata row)
    {
        var mapping = SqlServerTypeMapper.Map(
            new SqlServerColumnTypeMetadata(row.ProviderTypeName, row.MaxLength, row.Precision, row.Scale));

        return new TechnicalField(
            row.ColumnName,
            row.ColumnId,
            mapping.CanonicalType,
            mapping.ProviderType,
            row.IsNullable,
            row.ColumnDescription,
            row.IsIdentity,
            row.IsComputed,
            row.IsPersistedComputed,
            row.GeneratedAlwaysType == 1,
            row.GeneratedAlwaysType == 2,
            row.SystemTypeId == 189);
    }
}
