using System.ComponentModel;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<SpikeTools>();

var app = builder.Build();
app.MapMcp("/mcp");
app.Run();

[McpServerToolType]
public sealed class SpikeTools
{
    [McpServerTool(
        Name = "echo_structured",
        Title = "Structured echo",
        UseStructuredContent = true)]
    [Description("Returns the supplied message in structured MCP content.")]
    public static EchoResult EchoStructured(
        [Description("A message to return.")] string message)
        => new(message, message.Length);
}

public sealed record EchoResult(string Message, int Length);
