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

        Console.WriteLine("Placeholder for Database Connector, DQL Validator, and MCP Server Initialization.");
        Console.WriteLine("Tools to be registered: list_tables, get_table_schema, execute_dql_query.");
    }
}