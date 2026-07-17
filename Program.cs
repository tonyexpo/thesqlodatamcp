using System;
using System.Threading.Tasks;
using TheSqlODataMCP;

namespace TheSqlODataMCP;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("SQL OData MCP Connector v1.0.0 initializing...");
        
        try
        {
            var settingsManager = new SettingsManager("settings.json");
            Console.WriteLine("Settings loaded successfully.");
            Console.WriteLine($"Bearer Token: {settingsManager.GetBearerToken()}");
            Console.WriteLine($"SQL Connection String: {settingsManager.GetSqlConnectionString()}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}");
        }

        Console.WriteLine("Placeholder for MCP Server Initialization and Tools Registration.");
    }
}