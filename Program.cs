using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using ModelContextProtocol.Configuration;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using TheSqlODataMCP;

namespace TheSqlODataMCP;

class Program
{
    static async Task Main(string[] args)
    {
        // Load settings and validate bearer token first
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
        
        // Use Microsoft.Extensions.Hosting's generic host pattern
        var builder = Host.CreateApplicationBuilder(args);

        // Register services
        builder.Services.AddSingleton(databaseConnector);
        builder.Services.AddSingleton(dqlValidator);
        builder.Services.AddSingleton<McpTools>();

        // Configure and add McpServer
        builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation 
            { 
                Name = "thesqlodatamcp", 
                Version = "1.0.0" 
            };
        })
        .WithStdioServerTransport()
        .WithToolsFromAssembly(typeof(McpTools).Assembly);

        // Build the host
        var host = builder.Build();

        Console.WriteLine("SQL OData MCP Connector v1.0.0 initializing...");
        Console.WriteLine("MCP Server started over stdio with tools: list_tables, get_table_schema, execute_dql_query.");
        
        // Run the host
        await host.RunAsync();
    }
}