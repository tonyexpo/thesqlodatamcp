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
            [o].[type] AS [ObjectType],
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

        return SqlServerCatalogProjection.CreateCatalog(rows);
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
}
