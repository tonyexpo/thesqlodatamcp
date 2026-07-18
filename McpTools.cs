using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TheSqlODataMCP;

namespace TheSqlODataMCP;

public class McpTools
{
    private readonly DatabaseConnector _databaseConnector;
    private readonly DqlValidator _dqlValidator;

    public McpTools(DatabaseConnector databaseConnector, DqlValidator dqlValidator)
    {
        _databaseConnector = databaseConnector;
        _dqlValidator = dqlValidator;
    }

    public async Task<List<string>> ListTablesAsync()
    {
        return _databaseConnector.ListTables();
    }

    public async Task<List<(string columnName, string dataType)>> GetTableSchemaAsync(string tableName)
    {
        return _databaseConnector.GetTableSchema(tableName);
    }

    public async Task<string> ExecuteDqlQueryAsync(string tableName, string whereConditionsJsonOrSql)
    {
        // Construct the DQL query based on table name and conditions
        // Use bracketed table name to prevent SQL injection via table name
        string query = $"SELECT * FROM [{tableName}]";
        
        System.Collections.Generic.Dictionary<string, object> parameters = new System.Collections.Generic.Dictionary<string, object>();
        int paramIndex = 1;

        if (!string.IsNullOrWhiteSpace(whereConditionsJsonOrSql))
        {
            // Try to parse as JSON or process as SQL snippet
            string trimmedConditions = whereConditionsJsonOrSql.Trim();
            
            if (trimmedConditions.StartsWith("{") && trimmedConditions.EndsWith("}"))
            {
                // JSON conditions: e.g., {"status": "active", "age": 30}
                try
                {
                    string jsonConditions = trimmedConditions;
                    var matches = System.Text.RegularExpressions.Regex.Matches(jsonConditions, @"\"([a-zA-Z_0-9]+)\"\s*:\s*(?:\"([^\"]+)\"|(\\d+))");
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        string key = match.Groups[1].Value;
                        string valueStr = match.Groups[2].Value != null ? match.Groups[2].Value : match.Groups[3].Value;
                        
                        if (!query.Contains("WHERE"))
                        {
                            query += " WHERE ";
                        }
                        else
                        {
                            query += " AND ";
                        }
                        
                        string paramKey = $"@p{paramIndex++}";
                        query += $"[{key}] = {paramKey}";
                        parameters[paramKey] = valueStr;
                    }
                }
                catch (Exception)
                {
                    throw new ArgumentException("Invalid JSON conditions format.");
                }
            }
            else if (trimmedConditions.ToUpperInvariant().StartsWith("WHERE"))
            {
                // SQL snippet starting with WHERE, append it
                query += " " + trimmedConditions;
            }
            else
            {
                throw new ArgumentException("Invalid whereConditionsJsonOrSql format.");
            }
        }

        // Validate the DQL query
        _dqlValidator.IsValidDql(query);

        // Execute query using DatabaseConnector
        return _databaseConnector.ExecuteDqlQuery(query, parameters);
    }
}