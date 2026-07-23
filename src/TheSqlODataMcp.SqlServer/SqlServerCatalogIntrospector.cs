using System.Data;
using Microsoft.Data.SqlClient;
using TheSqlODataMcp.Core.Catalog;

namespace TheSqlODataMcp.SqlServer;

/// <summary>
/// Discovers the physical SQL Server tables, views, and columns that form a technical catalog.
/// </summary>
public sealed class SqlServerCatalogIntrospector
{
    private const string CatalogQuery = """
        SELECT
            [o].[object_id] AS [ObjectId],
            [s].[name] AS [SchemaName],
            [o].[name] AS [ObjectName],
            CONVERT(varchar(1), [o].[type]) AS [ObjectType],
            COALESCE([t].[temporal_type], 0) AS [TemporalType],
            [c].[name] AS [ColumnName],
            [c].[column_id] AS [ColumnId],
            [c].[is_nullable] AS [IsNullable],
            [c].[is_identity] AS [IsIdentity],
            [c].[is_computed] AS [IsComputed],
            COALESCE([cc].[is_persisted], CONVERT(bit, 0)) AS [IsPersistedComputed],
            [c].[generated_always_type] AS [GeneratedAlwaysType],
            [c].[system_type_id] AS [SystemTypeId],
            [ty].[name] AS [ProviderTypeName],
            [c].[max_length] AS [MaxLength],
            [c].[precision] AS [Precision],
            [c].[scale] AS [Scale],
            CONVERT(nvarchar(max), [object_description].[value]) AS [ObjectDescription],
            CONVERT(nvarchar(max), [column_description].[value]) AS [ColumnDescription]
        FROM [sys].[objects] AS [o]
        INNER JOIN [sys].[schemas] AS [s]
            ON [s].[schema_id] = [o].[schema_id]
        INNER JOIN [sys].[columns] AS [c]
            ON [c].[object_id] = [o].[object_id]
        INNER JOIN [sys].[types] AS [ty]
            ON [ty].[user_type_id] = [c].[user_type_id]
        LEFT JOIN [sys].[tables] AS [t]
            ON [t].[object_id] = [o].[object_id]
        LEFT JOIN [sys].[computed_columns] AS [cc]
            ON [cc].[object_id] = [c].[object_id]
            AND [cc].[column_id] = [c].[column_id]
        LEFT JOIN [sys].[extended_properties] AS [object_description]
            ON [object_description].[class] = 1
            AND [object_description].[major_id] = [o].[object_id]
            AND [object_description].[minor_id] = 0
            AND [object_description].[name] = N'MS_Description'
        LEFT JOIN [sys].[extended_properties] AS [column_description]
            ON [column_description].[class] = 1
            AND [column_description].[major_id] = [o].[object_id]
            AND [column_description].[minor_id] = [c].[column_id]
            AND [column_description].[name] = N'MS_Description'
        WHERE [o].[type] IN ('U', 'V')
            AND [o].[is_ms_shipped] = 0
            AND [s].[name] NOT IN (N'sys', N'INFORMATION_SCHEMA')
            AND COALESCE([t].[temporal_type], 0) <> 1
        ORDER BY [s].[name], [o].[name], [c].[column_id];

        SELECT
            [metadata].[ObjectId],
            [metadata].[MetadataKind],
            [metadata].[MetadataName],
            [metadata].[IsPrimary],
            [metadata].[IsUnique],
            [metadata].[IsFiltered],
            [metadata].[KeyOrdinal],
            [metadata].[ColumnName]
        FROM
        (
            SELECT
                [kc].[parent_object_id] AS [ObjectId],
                N'K' AS [MetadataKind],
                [kc].[name] AS [MetadataName],
                CONVERT(bit, CASE WHEN [kc].[type] = 'PK' THEN 1 ELSE 0 END) AS [IsPrimary],
                CONVERT(bit, 1) AS [IsUnique],
                CONVERT(bit, 0) AS [IsFiltered],
                CONVERT(int, [ic].[key_ordinal]) AS [KeyOrdinal],
                [c].[name] AS [ColumnName]
            FROM [sys].[key_constraints] AS [kc]
            INNER JOIN [sys].[objects] AS [o]
                ON [o].[object_id] = [kc].[parent_object_id]
            INNER JOIN [sys].[schemas] AS [s]
                ON [s].[schema_id] = [o].[schema_id]
            INNER JOIN [sys].[tables] AS [t]
                ON [t].[object_id] = [o].[object_id]
            INNER JOIN [sys].[index_columns] AS [ic]
                ON [ic].[object_id] = [kc].[parent_object_id]
                AND [ic].[index_id] = [kc].[unique_index_id]
                AND [ic].[key_ordinal] > 0
            INNER JOIN [sys].[columns] AS [c]
                ON [c].[object_id] = [ic].[object_id]
                AND [c].[column_id] = [ic].[column_id]
            WHERE [kc].[type] IN ('PK', 'UQ')
                AND [o].[is_ms_shipped] = 0
                AND [s].[name] NOT IN (N'sys', N'INFORMATION_SCHEMA')
                AND [t].[temporal_type] <> 1

            UNION ALL

            SELECT
                [i].[object_id] AS [ObjectId],
                N'I' AS [MetadataKind],
                [i].[name] AS [MetadataName],
                CONVERT(bit, 0) AS [IsPrimary],
                [i].[is_unique] AS [IsUnique],
                [i].[has_filter] AS [IsFiltered],
                CONVERT(int, [ic].[key_ordinal]) AS [KeyOrdinal],
                [c].[name] AS [ColumnName]
            FROM [sys].[indexes] AS [i]
            INNER JOIN [sys].[objects] AS [o]
                ON [o].[object_id] = [i].[object_id]
            INNER JOIN [sys].[schemas] AS [s]
                ON [s].[schema_id] = [o].[schema_id]
            LEFT JOIN [sys].[tables] AS [t]
                ON [t].[object_id] = [o].[object_id]
            INNER JOIN [sys].[index_columns] AS [ic]
                ON [ic].[object_id] = [i].[object_id]
                AND [ic].[index_id] = [i].[index_id]
                AND [ic].[key_ordinal] > 0
                AND [ic].[is_included_column] = 0
            INNER JOIN [sys].[columns] AS [c]
                ON [c].[object_id] = [ic].[object_id]
                AND [c].[column_id] = [ic].[column_id]
            LEFT JOIN [sys].[key_constraints] AS [kc]
                ON [kc].[parent_object_id] = [i].[object_id]
                AND [kc].[unique_index_id] = [i].[index_id]
            WHERE [o].[type] IN ('U', 'V')
                AND [o].[is_ms_shipped] = 0
                AND [s].[name] NOT IN (N'sys', N'INFORMATION_SCHEMA')
                AND COALESCE([t].[temporal_type], 0) <> 1
                AND [kc].[object_id] IS NULL
                AND [i].[index_id] > 0
                AND [i].[type] IN (1, 2)
                AND [i].[is_hypothetical] = 0
                AND [i].[is_disabled] = 0
        ) AS [metadata]
        ORDER BY [metadata].[ObjectId], [metadata].[MetadataKind], [metadata].[MetadataName], [metadata].[KeyOrdinal];

        SELECT
            [fk].[parent_object_id] AS [SourceObjectId],
            [fk].[name] AS [ForeignKeyName],
            [fk].[referenced_object_id] AS [TargetObjectId],
            [target_schema].[name] AS [TargetSchemaName],
            [target].[name] AS [TargetObjectName],
            [fkc].[constraint_column_id] AS [PairOrdinal],
            [source_column].[name] AS [SourceColumnName],
            [target_column].[name] AS [TargetColumnName]
        FROM [sys].[foreign_keys] AS [fk]
        INNER JOIN [sys].[foreign_key_columns] AS [fkc]
            ON [fkc].[constraint_object_id] = [fk].[object_id]
        INNER JOIN [sys].[objects] AS [source]
            ON [source].[object_id] = [fk].[parent_object_id]
        INNER JOIN [sys].[schemas] AS [source_schema]
            ON [source_schema].[schema_id] = [source].[schema_id]
        INNER JOIN [sys].[tables] AS [source_table]
            ON [source_table].[object_id] = [source].[object_id]
        INNER JOIN [sys].[columns] AS [source_column]
            ON [source_column].[object_id] = [fkc].[parent_object_id]
            AND [source_column].[column_id] = [fkc].[parent_column_id]
        INNER JOIN [sys].[objects] AS [target]
            ON [target].[object_id] = [fk].[referenced_object_id]
        INNER JOIN [sys].[schemas] AS [target_schema]
            ON [target_schema].[schema_id] = [target].[schema_id]
        INNER JOIN [sys].[tables] AS [target_table]
            ON [target_table].[object_id] = [target].[object_id]
        INNER JOIN [sys].[columns] AS [target_column]
            ON [target_column].[object_id] = [fkc].[referenced_object_id]
            AND [target_column].[column_id] = [fkc].[referenced_column_id]
        WHERE [source].[is_ms_shipped] = 0
            AND [source_schema].[name] NOT IN (N'sys', N'INFORMATION_SCHEMA')
            AND [source_table].[temporal_type] <> 1
            AND [target].[is_ms_shipped] = 0
            AND [target_schema].[name] NOT IN (N'sys', N'INFORMATION_SCHEMA')
            AND [target_table].[temporal_type] <> 1
        ORDER BY [fk].[parent_object_id], [fk].[name], [fkc].[constraint_column_id];
        """;

    private readonly string connectionString;
    private readonly int commandTimeoutSeconds;

    /// <summary>
    /// Creates a SQL Server catalog introspector.
    /// </summary>
    public SqlServerCatalogIntrospector(string connectionString, int commandTimeoutSeconds = 30)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (commandTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(commandTimeoutSeconds), "The command timeout must be positive.");
        }

        this.connectionString = connectionString;
        this.commandTimeoutSeconds = commandTimeoutSeconds;
    }

    /// <summary>
    /// Asynchronously discovers the current SQL Server technical catalog.
    /// </summary>
    public async Task<TechnicalCatalog> IntrospectAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = CatalogQuery;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = commandTimeoutSeconds;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);
        var rows = new List<SqlServerCatalogColumnMetadata>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(ReadColumnMetadata(reader));
        }

        if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The SQL Server catalog query did not return key and index metadata.");
        }

        var keyAndIndexRows = new List<SqlServerCatalogKeyOrIndexMetadata>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            keyAndIndexRows.Add(ReadKeyOrIndexMetadata(reader));
        }

        if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The SQL Server catalog query did not return foreign-key metadata.");
        }

        var foreignKeyRows = new List<SqlServerCatalogForeignKeyMetadata>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            foreignKeyRows.Add(ReadForeignKeyMetadata(reader));
        }

        if (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The SQL Server catalog query returned an unexpected result set.");
        }

        return SqlServerCatalogProjection.CreateCatalog(rows, keyAndIndexRows, foreignKeyRows);
    }

    private static SqlServerCatalogColumnMetadata ReadColumnMetadata(SqlDataReader reader) =>
        new(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4),
            reader.GetString(5),
            reader.GetInt32(6),
            reader.GetBoolean(7),
            reader.GetBoolean(8),
            reader.GetBoolean(9),
            reader.GetBoolean(10),
            reader.GetByte(11),
            reader.GetByte(12),
            reader.GetString(13),
            reader.GetInt16(14),
            reader.GetByte(15),
            reader.GetByte(16),
            reader.IsDBNull(17) ? null : reader.GetString(17),
            reader.IsDBNull(18) ? null : reader.GetString(18));

    private static SqlServerCatalogKeyOrIndexMetadata ReadKeyOrIndexMetadata(SqlDataReader reader) =>
        new(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetBoolean(3),
            reader.GetBoolean(4),
            reader.GetBoolean(5),
            reader.GetInt32(6),
            reader.GetString(7));

    private static SqlServerCatalogForeignKeyMetadata ReadForeignKeyMetadata(SqlDataReader reader) =>
        new(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5),
            reader.GetString(6),
            reader.GetString(7));
}
