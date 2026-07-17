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

        var databaseConnector = new DatabaseConnector(settings.SqlConnectionString);
        var dqlValidator = new DqlValidator();
        var mcpTools = new McpTools(databaseConnector, dqlValidator);

        Console.WriteLine("Placeholder for MCP Server Initialization and Tools Registration.");
        Console.WriteLine("Tools to be registered: list_tables, get_table_schema, execute_dql_query.");
        
        // Placeholder for actual server transport and authentication initialization using ModelContextProtocol SDK
    }
}