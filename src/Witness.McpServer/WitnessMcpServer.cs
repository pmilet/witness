using System.Text.Json;
using StreamJsonRpc;
using Witness.McpServer.Handlers;
using Witness.McpServer.Models;

namespace Witness.McpServer;

/// <summary>
/// MCP Server implementation using JSON-RPC
/// </summary>
public sealed class WitnessMcpServer
{
    private readonly RecordToolHandler _recordHandler;
    private readonly ReplayToolHandler _replayHandler;
    private readonly InspectToolHandler _inspectHandler;
    private readonly ListToolHandler _listHandler;

    public WitnessMcpServer(
        RecordToolHandler recordHandler,
        ReplayToolHandler replayHandler,
        InspectToolHandler inspectHandler,
        ListToolHandler listHandler)
    {
        _recordHandler = recordHandler ?? throw new ArgumentNullException(nameof(recordHandler));
        _replayHandler = replayHandler ?? throw new ArgumentNullException(nameof(replayHandler));
        _inspectHandler = inspectHandler ?? throw new ArgumentNullException(nameof(inspectHandler));
        _listHandler = listHandler ?? throw new ArgumentNullException(nameof(listHandler));
    }

    [JsonRpcMethod("initialize")]
    public Task<InitializeResponse> InitializeAsync(InitializeRequest request)
    {
        return Task.FromResult(new InitializeResponse
        {
            ProtocolVersion = "2024-11-05",
            ServerInfo = new ServerInfo
            {
                Name = "witness-mcp-server",
                Version = "1.0.0"
            },
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability()
            }
        });
    }

    [JsonRpcMethod("tools/list")]
    public Task<ToolsListResponse> ListToolsAsync()
    {
        return Task.FromResult(new ToolsListResponse
        {
            Tools = new List<Tool>
            {
                new Tool
                {
                    Name = "witness/record",
                    Description = "Execute an HTTP request and capture the full interaction",
                    InputSchema = new InputSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, object>
                        {
                            ["target"] = new { type = "string", description = "Target URL (e.g., https://api.example.com)" },
                            ["method"] = new { type = "string", description = "HTTP method (GET, POST, PUT, DELETE, PATCH)" },
                            ["path"] = new { type = "string", description = "Request path (e.g., /api/users)" },
                            ["headers"] = new { type = "object", description = "Request headers" },
                            ["body"] = new { type = "string", description = "Request body (JSON string)" },
                            ["options"] = new { type = "object", description = "Recording options (tag, sessionId, etc.)" }
                        },
                        Required = new List<string> { "target", "method", "path" }
                    }
                },
                new Tool
                {
                    Name = "witness/replay",
                    Description = "Replay a recorded interaction against a different target",
                    InputSchema = new InputSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, object>
                        {
                            ["witnessId"] = new { type = "string", description = "ID of the interaction to replay" },
                            ["target"] = new { type = "string", description = "New target URL" },
                            ["options"] = new { type = "object", description = "Replay options" }
                        },
                        Required = new List<string> { "witnessId", "target" }
                    }
                },
                new Tool
                {
                    Name = "witness/inspect",
                    Description = "View details of a recorded interaction",
                    InputSchema = new InputSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, object>
                        {
                            ["witnessId"] = new { type = "string", description = "ID of the interaction to inspect" },
                            ["sessionId"] = new { type = "string", description = "Session ID containing the interaction" }
                        },
                        Required = new List<string> { "witnessId", "sessionId" }
                    }
                },
                new Tool
                {
                    Name = "witness/list-sessions",
                    Description = "List all recorded sessions",
                    InputSchema = new InputSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, object>()
                    }
                },
                new Tool
                {
                    Name = "witness/list-interactions",
                    Description = "List all interactions in a session",
                    InputSchema = new InputSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, object>
                        {
                            ["sessionId"] = new { type = "string", description = "Session ID to list interactions from" }
                        },
                        Required = new List<string> { "sessionId" }
                    }
                }
            }
        });
    }

    [JsonRpcMethod("tools/call")]
    public async Task<ToolCallResponse> CallToolAsync(ToolCallRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        try
        {
            var argumentsJson = JsonSerializer.Serialize(request.Arguments);
            string result = request.Name switch
            {
                "witness/record" => await _recordHandler.HandleAsync(argumentsJson, cancellationToken),
                "witness/replay" => await _replayHandler.HandleAsync(argumentsJson, cancellationToken),
                "witness/inspect" => await _inspectHandler.HandleAsync(argumentsJson, cancellationToken),
                "witness/list-sessions" => await _listHandler.HandleSessionsAsync(cancellationToken),
                "witness/list-interactions" => await _listHandler.HandleInteractionsAsync(argumentsJson, cancellationToken),
                _ => JsonSerializer.Serialize(new { success = false, error = $"Unknown tool: {request.Name}" })
            };

            return new ToolCallResponse
            {
                Content = new List<Content>
                {
                    new Content
                    {
                        Type = "text",
                        Text = result
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new ToolCallResponse
            {
                Content = new List<Content>
                {
                    new Content
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(new { success = false, error = ex.Message })
                    }
                },
                IsError = true
            };
        }
    }

    [JsonRpcMethod("ping")]
    public Task<object> PingAsync()
    {
        return Task.FromResult<object>(new { });
    }
}
