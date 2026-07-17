using System;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace TheSqlODataMcp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("SQL OData MCP Connector starting...");
            
            // TODO: Load settings and bearer token
            // TODO: Initialize database connection
            
            var server = new McpServer(
                new ServerInfo
                {
                    Name = "SqlODataMcpConnector",
                    Version = "1.0.0"
                }
            );

            // Register MCP Tools
            // TODO: Implement list_tables, get_table_schema, execute_dql_query
            
            Console.WriteLine("MCP Server initialized and ready.");
            
            // TODO: Start MCP server (stdio or SSE)
        }
    }
}
