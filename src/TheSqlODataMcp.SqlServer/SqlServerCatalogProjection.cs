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

internal sealed record SqlServerCatalogKeyOrIndexMetadata(
    int ObjectId,
    string MetadataKind,
    string MetadataName,
    bool IsPrimary,
    bool IsUnique,
    bool IsFiltered,
    int KeyOrdinal,
    string ColumnName);

internal sealed record SqlServerCatalogForeignKeyMetadata(
    int SourceObjectId,
    string ForeignKeyName,
    int TargetObjectId,
    string TargetSchemaName,
    string TargetObjectName,
    int PairOrdinal,
    string SourceColumnName,
    string TargetColumnName);

internal static class SqlServerCatalogProjection
{
    internal static TechnicalCatalog CreateCatalog(
        IEnumerable<SqlServerCatalogColumnMetadata> rows,
        IEnumerable<SqlServerCatalogKeyOrIndexMetadata>? keyAndIndexRows = null,
        IEnumerable<SqlServerCatalogForeignKeyMetadata>? foreignKeyRows = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        keyAndIndexRows ??= Array.Empty<SqlServerCatalogKeyOrIndexMetadata>();
        foreignKeyRows ??= Array.Empty<SqlServerCatalogForeignKeyMetadata>();

        var objectRows = rows
            .Select(ValidateRow)
            .GroupBy(static row => row.ObjectId)
            .ToDictionary(static group => group.Key);
        var keyAndIndexByObjectId = keyAndIndexRows
            .Select(ValidateKeyOrIndexRow)
            .GroupBy(static row => row.ObjectId)
            .ToDictionary(static group => group.Key, static group => (IEnumerable<SqlServerCatalogKeyOrIndexMetadata>)group);
        var foreignKeysBySourceObjectId = foreignKeyRows
            .Select(ValidateForeignKeyRow)
            .GroupBy(static row => row.SourceObjectId)
            .ToDictionary(static group => group.Key, static group => (IEnumerable<SqlServerCatalogForeignKeyMetadata>)group);

        RejectOrphanMetadata(keyAndIndexByObjectId.Keys, objectRows, "key/index");
        RejectOrphanMetadata(foreignKeysBySourceObjectId.Keys, objectRows, "foreign-key source");

        var entities = objectRows
            .Select(pair => CreateEntity(
                pair.Value,
                keyAndIndexByObjectId.GetValueOrDefault(pair.Key, Array.Empty<SqlServerCatalogKeyOrIndexMetadata>()),
                foreignKeysBySourceObjectId.GetValueOrDefault(pair.Key, Array.Empty<SqlServerCatalogForeignKeyMetadata>()),
                objectRows))
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

    private static SqlServerCatalogKeyOrIndexMetadata ValidateKeyOrIndexRow(SqlServerCatalogKeyOrIndexMetadata row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.ObjectId <= 0 || row.KeyOrdinal <= 0)
        {
            throw new InvalidOperationException("SQL Server key and index identifiers must be positive.");
        }

        if (row.MetadataKind is not ("K" or "I"))
        {
            throw new InvalidOperationException("SQL Server key/index metadata kind is not supported.");
        }

        if (row.MetadataKind == "K" && row.IsFiltered)
        {
            throw new InvalidOperationException("SQL Server key constraints cannot be filtered.");
        }

        if (row.MetadataKind == "K" && !row.IsUnique)
        {
            throw new InvalidOperationException("SQL Server key constraints must be unique.");
        }

        if (row.MetadataKind == "I" && row.IsPrimary)
        {
            throw new InvalidOperationException("Standalone SQL Server indexes cannot be primary keys.");
        }

        return row;
    }

    private static SqlServerCatalogForeignKeyMetadata ValidateForeignKeyRow(SqlServerCatalogForeignKeyMetadata row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.SourceObjectId <= 0 || row.TargetObjectId <= 0 || row.PairOrdinal <= 0)
        {
            throw new InvalidOperationException("SQL Server foreign-key identifiers must be positive.");
        }

        return row;
    }

    private static TechnicalEntity CreateEntity(
        IGrouping<int, SqlServerCatalogColumnMetadata> objectRows,
        IEnumerable<SqlServerCatalogKeyOrIndexMetadata> keyAndIndexRows,
        IEnumerable<SqlServerCatalogForeignKeyMetadata> foreignKeyRows,
        Dictionary<int, IGrouping<int, SqlServerCatalogColumnMetadata>> allObjectRows)
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

        var keys = keyAndIndexRows
            .Where(static row => row.MetadataKind == "K")
            .GroupBy(static row => row.MetadataName, StringComparer.Ordinal)
            .Select(group =>
            {
                EnsureOrderedColumns(group.Select(static row => (row.KeyOrdinal, row.ColumnName)), "key");
                var firstRow = group.First();
                if (group.Any(row => row.IsPrimary != firstRow.IsPrimary || row.IsUnique != firstRow.IsUnique || row.IsFiltered != firstRow.IsFiltered))
                {
                    throw new InvalidOperationException("SQL Server key metadata is inconsistent.");
                }

                return new CatalogKey(
                    firstRow.MetadataName,
                    group.OrderBy(static row => row.KeyOrdinal).Select(static row => row.ColumnName),
                    firstRow.IsPrimary);
            })
            .OrderBy(static key => key.Name, StringComparer.Ordinal)
            .ToArray();

        var indexes = keyAndIndexRows
            .Where(static row => row.MetadataKind == "I")
            .GroupBy(static row => row.MetadataName, StringComparer.Ordinal)
            .Select(group =>
            {
                EnsureOrderedColumns(group.Select(static row => (row.KeyOrdinal, row.ColumnName)), "index");
                var firstRow = group.First();
                if (group.Any(row => row.IsPrimary != firstRow.IsPrimary || row.IsUnique != firstRow.IsUnique || row.IsFiltered != firstRow.IsFiltered))
                {
                    throw new InvalidOperationException("SQL Server index metadata is inconsistent.");
                }

                return new CatalogIndex(
                    firstRow.MetadataName,
                    group.OrderBy(static row => row.KeyOrdinal).Select(static row => row.ColumnName),
                    firstRow.IsUnique,
                    isFiltered: firstRow.IsFiltered);
            })
            .OrderBy(static index => index.Name, StringComparer.Ordinal)
            .ToArray();

        var relationships = foreignKeyRows
            .GroupBy(static row => row.ForeignKeyName, StringComparer.Ordinal)
            .Select(group =>
            {
                EnsureOrderedColumns(group.Select(static row => (row.PairOrdinal, row.SourceColumnName)), "foreign-key");
                var firstRow = group.First();
                if (group.Any(row => row.TargetObjectId != firstRow.TargetObjectId
                    || !string.Equals(row.TargetSchemaName, firstRow.TargetSchemaName, StringComparison.Ordinal)
                    || !string.Equals(row.TargetObjectName, firstRow.TargetObjectName, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("SQL Server foreign-key target metadata is inconsistent.");
                }

                if (!allObjectRows.TryGetValue(firstRow.TargetObjectId, out var targetRows))
                {
                    throw new InvalidOperationException("SQL Server foreign-key target is not part of the technical catalog.");
                }

                var targetFirst = targetRows.First();
                if (!string.Equals(targetFirst.SchemaName, firstRow.TargetSchemaName, StringComparison.Ordinal)
                    || !string.Equals(targetFirst.ObjectName, firstRow.TargetObjectName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("SQL Server foreign-key target identity is inconsistent.");
                }

                var targetFieldNames = new HashSet<string>(targetRows.Select(static row => row.ColumnName), StringComparer.Ordinal);
                var pairs = group.OrderBy(static row => row.PairOrdinal).Select(row =>
                {
                    if (!targetFieldNames.Contains(row.TargetColumnName))
                    {
                        throw new InvalidOperationException("SQL Server foreign-key target field is not part of the technical catalog.");
                    }

                    return new RelationshipFieldPair(row.SourceColumnName, row.TargetColumnName);
                });

                return new CatalogRelationship(
                    firstRow.ForeignKeyName,
                    new PhysicalObjectIdentity(firstRow.TargetSchemaName, firstRow.TargetObjectName),
                    pairs);
            })
            .OrderBy(static relationship => relationship.Name, StringComparer.Ordinal)
            .ToArray();

        return new TechnicalEntity(
            new PhysicalObjectIdentity(first.SchemaName, first.ObjectName),
            first.ObjectType == "U" ? CatalogObjectKind.Table : CatalogObjectKind.View,
            fields,
            keys,
            indexes,
            relationships,
            description: first.ObjectDescription,
            isTemporal: first.TemporalType == 2);
    }

    private static void EnsureOrderedColumns(IEnumerable<(int Ordinal, string Name)> columns, string metadataKind)
    {
        var orderedColumns = columns.OrderBy(static column => column.Ordinal).ToArray();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < orderedColumns.Length; index++)
        {
            var (ordinal, name) = orderedColumns[index];
            if (ordinal != index + 1 || !seenNames.Add(name))
            {
                throw new InvalidOperationException($"SQL Server {metadataKind} column ordering is inconsistent.");
            }
        }
    }

    private static void RejectOrphanMetadata(
        IEnumerable<int> objectIds,
        Dictionary<int, IGrouping<int, SqlServerCatalogColumnMetadata>> objectRows,
        string metadataKind)
    {
        foreach (var objectId in objectIds)
        {
            if (!objectRows.ContainsKey(objectId))
            {
                throw new InvalidOperationException($"SQL Server {metadataKind} metadata is not part of the technical catalog.");
            }
        }
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
