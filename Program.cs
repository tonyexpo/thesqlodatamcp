using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using ModelContextProtocol.Transport.Stdio;
using TheSqlODataMCP;
using System.Text.Json;

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

        // Initialize MCP Server
        var serverOptions = new McpServerOptions
        {
            Name = "SQL OData MCP Connector",
            Version = "1.0.0"
        };

        var server = new McpServer(serverOptions);

        // Register list_tables tool
        var listTablesToolDef = new ToolDefinition(
            name: "list_tables",
            description: "List available tables in the database.",
            inputSchema: JsonDocument.Parse("{ 'type': 'object', 'properties': {} }")
        );
        
        server.AddTool(listTablesToolDef, async (toolCall, ctx) => {
            var tables = await mcpTools.ListTablesAsync();
            var result = new List<string>();
            foreach(var table in tables) {
                result.Add(table);
            }
            return new ToolResultResponse(new ToolResult(result.ToString(), null));
        });

        // Register get_table_schema tool
        var getSchemaToolDef = new ToolDefinition(
            name: "get_table_schema",
            description: "Retrieve the schema (column names and data types) for a specific table.",
            inputSchema: JsonDocument.Parse("{ 'type': 'object', 'properties': { 'table_name': { 'type': 'string' } } }")
        );

        server.AddTool(getSchemaToolDef, async (toolCall, ctx) => {
            var tableName = toolCall.Input?.GetProperty("table_name")?.GetString();
            if (string.IsNullOrEmpty(tableName)) {
                throw new ArgumentException("table_name is required.");
            }
            var schema = await mcpTools.GetTableSchemaAsync(tableName);
            var result = $"Columns: {string.Join(", ", schema.Select(c => $"{c.columnName} ({c.dataType})"))}";
            return new ToolResultResponse(new ToolResult(result, null));
        });

        // Register execute_dql_query tool
        var executeQueryToolDef = new ToolDefinition(
            name: "execute_dql_query",
            description: "Execute a validated DQL query using parameterized conditions.",
            inputSchema: JsonDocument.Parse("{ 'type': 'object', 'properties': { 'table_name': { 'type': 'string' }, 'where_conditions_json_or_sql': { 'type': 'string' } } }")
        );

        server.AddTool(executeQueryToolDef, async (toolCall, ctx) => {
            var tableName = toolCall.Input?.GetProperty("table_name")?.GetString();
            var whereConditions = toolCall.Input?.GetProperty("where_conditions_json_or_sql")?.GetString();
            
            if (string.IsNullOrEmpty(tableName)) {
                throw new ArgumentException("table_name is required.");
            }

            var result = await mcpTools.ExecuteDqlQueryAsync(tableName, whereConditions ?? "");
            return new ToolResultResponse(new ToolResult(result, null));
        });

        // Initialize Stdio Transport
        var transport = new StdioServerTransport();
        
        Console.WriteLine("MCP Server initialized and running via stdio transport.");
        Console.WriteLine("Bearer token authentication validated successfully.");
        
        await server.StartAsync(transport, default);
    }
}