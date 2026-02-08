using System.Text.Json.Serialization;

namespace Witness.McpServer.Models;

public sealed class InitializeRequest
{
    [JsonPropertyName("protocolVersion")]
    public string? ProtocolVersion { get; set; }

    [JsonPropertyName("capabilities")]
    public object? Capabilities { get; set; }

    [JsonPropertyName("clientInfo")]
    public ClientInfo? ClientInfo { get; set; }
}

public sealed class ClientInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

public sealed class InitializeResponse
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = string.Empty;

    [JsonPropertyName("serverInfo")]
    public ServerInfo ServerInfo { get; set; } = new();

    [JsonPropertyName("capabilities")]
    public ServerCapabilities Capabilities { get; set; } = new();
}

public sealed class ServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

public sealed class ServerCapabilities
{
    [JsonPropertyName("tools")]
    public ToolsCapability? Tools { get; set; }
}

public sealed class ToolsCapability
{
}

public sealed class ToolsListResponse
{
    [JsonPropertyName("tools")]
    public List<Tool> Tools { get; set; } = new();
}

public sealed class Tool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public InputSchema InputSchema { get; set; } = new();
}

public sealed class InputSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    public List<string>? Required { get; set; }
}

public sealed class ToolCallRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object>? Arguments { get; set; }
}

public sealed class ToolCallResponse
{
    [JsonPropertyName("content")]
    public List<Content> Content { get; set; } = new();

    [JsonPropertyName("isError")]
    public bool? IsError { get; set; }
}

public sealed class Content
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
