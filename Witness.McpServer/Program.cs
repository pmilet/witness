using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Witness.Application;
using Witness.Application.Commands;
using Witness.Application.Queries;
using Witness.Infrastructure;
using Witness.McpServer.McpTools;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .Build();

// Build service provider
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConfiguration(configuration.GetSection("Logging"));
    builder.AddConsole();
});

// Add application and infrastructure layers
services.AddApplication();
services.AddInfrastructure(configuration);

var serviceProvider = services.BuildServiceProvider();

// Get logger
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

// Log to stderr so it doesn't interfere with MCP protocol on stdout
Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });

// Display alpha version warning
logger.LogWarning("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogWarning("⚠️  WITNESS MCP SERVER - ALPHA VERSION");
logger.LogWarning("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogWarning("This software is in ALPHA stage and under active development.");
logger.LogWarning("Features may change, and breaking changes may occur without notice.");
logger.LogWarning("Use in production environments at your own risk.");
logger.LogWarning("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

logger.LogInformation("Witness MCP Server starting...");

try
{
    await RunMcpServerAsync(serviceProvider, logger);
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error in MCP server");
    Environment.Exit(1);
}

static async Task RunMcpServerAsync(IServiceProvider serviceProvider, ILogger logger)
{
    var mediator = serviceProvider.GetRequiredService<IMediator>();

    // Read MCP protocol messages from stdin
    using var reader = new StreamReader(Console.OpenStandardInput());
    using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

    while (!reader.EndOfStream)
    {
        var line = await reader.ReadLineAsync();
        if (string.IsNullOrEmpty(line)) continue;

        try
        {
            var request = JsonSerializer.Deserialize<McpRequest>(line);
            if (request == null) continue;

            var response = await HandleRequestAsync(request, mediator, logger);
            var responseJson = JsonSerializer.Serialize(response);
            
            await writer.WriteLineAsync(responseJson);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling MCP request");
            
            var errorResponse = new McpResponse
            {
                jsonrpc = "2.0",
                id = null,
                error = new McpError
                {
                    code = -32603,
                    message = "Internal error",
                    data = ex.Message
                }
            };
            
            var errorJson = JsonSerializer.Serialize(errorResponse);
            await writer.WriteLineAsync(errorJson);
        }
    }
}

static async Task<McpResponse> HandleRequestAsync(McpRequest request, IMediator mediator, ILogger logger)
{
    // Handle different MCP methods
    switch (request.method)
    {
        case "tools/list":
            return new McpResponse
            {
                jsonrpc = "2.0",
                id = request.id,
                result = new { tools = McpToolDefinitions.Tools }
            };

        case "tools/call":
            return await HandleToolCallAsync(request, mediator, logger);

        case "initialize":
            return new McpResponse
            {
                jsonrpc = "2.0",
                id = request.id,
                result = new
                {
                    protocolVersion = "0.1.0-alpha",
                    serverInfo = new
                    {
                        name = "witness-mcp",
                        version = "0.1.0-alpha"
                    },
                    capabilities = new
                    {
                        tools = new { }
                    }
                }
            };

        case "initialized":
            return new McpResponse
            {
                jsonrpc = "2.0",
                id = request.id,
                result = new { }
            };

        case "ping":
            return new McpResponse
            {
                jsonrpc = "2.0",
                id = request.id,
                result = new { }
            };

        default:
            return new McpResponse
            {
                jsonrpc = "2.0",
                id = request.id,
                error = new McpError
                {
                    code = -32601,
                    message = $"Method not found: {request.method}"
                }
            };
    }
}

static async Task<McpResponse> HandleToolCallAsync(McpRequest request, IMediator mediator, ILogger logger)
{
    try
    {
        var toolParams = request.@params as JsonElement?;
        if (toolParams == null)
        {
            return CreateErrorResponse(request.id, -32602, "Invalid params");
        }

        var toolName = toolParams.Value.GetProperty("name").GetString();
        var arguments = toolParams.Value.GetProperty("arguments");

        logger.LogInformation("Handling tool call: {ToolName}", toolName);

        object? result = toolName switch
        {
            "witness/record" => await HandleRecordAsync(arguments, mediator),
            "witness/replay" => await HandleReplayAsync(arguments, mediator),
            "witness/inspect" => await HandleInspectAsync(arguments, mediator),
            "witness/list" => await HandleListAsync(arguments, mediator),
            _ => throw new InvalidOperationException($"Unknown tool: {toolName}")
        };

        return new McpResponse
        {
            jsonrpc = "2.0",
            id = request.id,
            result = new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            }
        };
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in tool call");
        return CreateErrorResponse(request.id, -32603, ex.Message);
    }
}

static async Task<object> HandleRecordAsync(JsonElement arguments, IMediator mediator)
{
    var command = new RecordInteractionCommand
    {
        Target = arguments.GetProperty("target").GetString() ?? "",
        Method = arguments.GetProperty("method").GetString() ?? "",
        Path = arguments.GetProperty("path").GetString() ?? "",
        Headers = arguments.TryGetProperty("headers", out var headers) 
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(headers.GetRawText())
            : null,
        Body = arguments.TryGetProperty("body", out var body) 
            ? JsonSerializer.Deserialize<object>(body.GetRawText())
            : null,
        Options = arguments.TryGetProperty("options", out var options)
            ? JsonSerializer.Deserialize<RecordOptions>(options.GetRawText())
            : null
    };

    return await mediator.Send(command);
}

static async Task<object> HandleReplayAsync(JsonElement arguments, IMediator mediator)
{
    var command = new ReplayInteractionCommand
    {
        WitnessId = arguments.GetProperty("witnessId").GetString() ?? "",
        Target = arguments.GetProperty("target").GetString() ?? "",
        Options = arguments.TryGetProperty("options", out var options)
            ? JsonSerializer.Deserialize<ReplayOptions>(options.GetRawText())
            : null
    };

    return await mediator.Send(command);
}

static async Task<object?> HandleInspectAsync(JsonElement arguments, IMediator mediator)
{
    var query = new InspectInteractionQuery
    {
        WitnessId = arguments.GetProperty("witnessId").GetString() ?? "",
        SessionId = arguments.TryGetProperty("sessionId", out var sessionId)
            ? sessionId.GetString()
            : null
    };

    return await mediator.Send(query);
}

static async Task<object> HandleListAsync(JsonElement arguments, IMediator mediator)
{
    if (arguments.TryGetProperty("sessionId", out var sessionIdProp))
    {
        var query = new ListInteractionsQuery
        {
            SessionId = sessionIdProp.GetString() ?? "",
            Limit = arguments.TryGetProperty("limit", out var limit) ? limit.GetInt32() : 50
        };
        return await mediator.Send(query);
    }
    else
    {
        var query = new ListSessionsQuery
        {
            Limit = arguments.TryGetProperty("limit", out var limit) ? limit.GetInt32() : 50
        };
        return await mediator.Send(query);
    }
}

static McpResponse CreateErrorResponse(object? id, int code, string message)
{
    return new McpResponse
    {
        jsonrpc = "2.0",
        id = id,
        error = new McpError
        {
            code = code,
            message = message
        }
    };
}

// MCP Protocol Models
public class McpRequest
{
    public string jsonrpc { get; set; } = "2.0";
    public object? id { get; set; }
    public string method { get; set; } = "";
    public object? @params { get; set; }
}

public class McpResponse
{
    public string jsonrpc { get; set; } = "2.0";
    public object? id { get; set; }
    public object? result { get; set; }
    public McpError? error { get; set; }
}

public class McpError
{
    public int code { get; set; }
    public string message { get; set; } = "";
    public object? data { get; set; }
}
