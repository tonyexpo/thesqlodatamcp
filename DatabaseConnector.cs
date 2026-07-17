using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace TheSqlODataMCP;

public class DatabaseConnector
{
    private readonly string _connectionString;

    public DatabaseConnector(string connectionString)
    {
        _connectionString = connectionString;
    }

    public List<string> ListTables()
    {
        var tables = new List<string>();
        
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            string query = @"
                SELECT t.name 
                FROM sys.tables t
                WHERE t.type = 'U' AND t.is_ms_shipped = 0;";

            using (var command = new SqlCommand(query, connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tables.Add(reader.GetString(0));
                    }
                }
            }
        }
        
        return tables;
    }

    public List<(string columnName, string dataType)> GetTableSchema(string tableName)
    {
        var schema = new List<(string columnName, string dataType)>();
        
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            string query = @"
                SELECT c.name, t.name AS dataType
                FROM sys.columns c
                INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE c.object_id = OBJECT_ID(@tableName);";

            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@tableName", tableName);
                
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string columnName = reader.GetString(0);
                        string dataType = reader.GetString(1);
                        schema.Add((columnName, dataType));
                    }
                }
            }
        }
        
        return schema;
    }
}