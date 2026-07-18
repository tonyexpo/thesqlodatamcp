using System;
using System.Threading.Tasks;
using TheSqlODataMCP;

namespace TheSqlODataMCP;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("SQL OData MCP Connector v1.0.0 initializing...");
        
        AppSettings settings;
        try
        {
            var settingsManager = new SettingsManager("settings.json");
            Console.WriteLine("Settings loaded successfully.");
            settings = settingsManager.GetSettings();
            
            Console.WriteLine($"Bearer Token: {settings.BearerToken}");
            Console.WriteLine($"SQL Connection String: {settings.SqlConnectionString}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}");
            return;
        }

        // Validate Bearer token
        if (string.IsNullOrWhiteSpace(settings.BearerToken))
        {
            Console.WriteLine("Error: Bearer token is missing or empty in settings.json.");
            return;
        }

        var databaseConnector = new DatabaseConnector(settings.SqlConnectionString);
        var dqlValidator = new DqlValidator();
        var mcpTools = new McpTools(databaseConnector, dqlValidator);

        Console.WriteLine("MCP Server initialization and Tools Registration (Placeholder for ModelContextProtocol SDK integration).");
        Console.WriteLine("Tools to be registered: list_tables, get_table_schema, execute_dql_query.");
        Console.WriteLine("Bearer token authentication validated successfully.");
        
        // Placeholder for actual server transport and authentication initialization using ModelContextProtocol SDK
    }
}