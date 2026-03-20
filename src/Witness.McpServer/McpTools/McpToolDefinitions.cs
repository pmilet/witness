namespace Witness.McpServer.McpTools;

public static class McpToolDefinitions
{
    public static readonly object[] Tools = new object[]
    {
        new
        {
            name = "witness/record",
            description = "Execute an HTTP request and capture the full interaction. Returns a WitnessId that can be used for replay and comparison.",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    target = new { type = "string", description = "Base URL of the target API (e.g., https://api.example.com)" },
                    method = new { type = "string", description = "HTTP method (GET, POST, PUT, DELETE, PATCH)", @enum = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" } },
                    path = new { type = "string", description = "Request path (e.g., /api/loans)" },
                    headers = new { type = "object", description = "HTTP headers as key-value pairs", additionalProperties = new { type = "string" } },
                    body = new { description = "Request body (JSON object, string, or omit for no body)" },
                    options = new
                    {
                        type = "object",
                        description = "Recording options",
                        properties = new
                        {
                            tag = new { type = "string", description = "Tag for this interaction (used in WitnessId)" },
                            sessionId = new { type = "string", description = "Session ID to group related interactions" },
                            description = new { type = "string", description = "Human-readable description of what this interaction tests" },
                            timeoutMs = new { type = "number", description = "Request timeout in milliseconds (default: 30000)" },
                            followRedirects = new { type = "boolean", description = "Whether to follow HTTP redirects (default: true)" }
                        }
                    }
                },
                required = new[] { "target", "method", "path" }
            }
        },
        new
        {
            name = "witness/replay",
            description = "Replay a previously recorded interaction against a different target.",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    witnessId = new { type = "string", description = "The WitnessId of the interaction to replay" },
                    target = new { type = "string", description = "New target URL to replay against" },
                    options = new
                    {
                        type = "object",
                        description = "Replay options",
                        properties = new
                        {
                            tag = new { type = "string", description = "Tag for the replay interaction" },
                            sessionId = new { type = "string", description = "Session ID for the replay" },
                            overrideHeaders = new { type = "object", description = "Headers to override in the replay", additionalProperties = new { type = "string" } }
                        }
                    }
                },
                required = new[] { "witnessId", "target" }
            }
        },
        new
        {
            name = "witness/inspect",
            description = "View the full details of a recorded interaction.",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    witnessId = new { type = "string", description = "The WitnessId to inspect" },
                    sessionId = new { type = "string", description = "Optional session ID to narrow the search" }
                },
                required = new[] { "witnessId" }
            }
        },
        new
        {
            name = "witness/list",
            description = "List recorded sessions or interactions within a session.",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    sessionId = new { type = "string", description = "Optional session ID to list interactions from a specific session" },
                    limit = new { type = "number", description = "Maximum number of results to return (default: 50)", @default = 50 }
                }
            }
        }
    };
}
