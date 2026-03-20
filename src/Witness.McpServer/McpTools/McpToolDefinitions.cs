using System.ComponentModel;
using System.Text.Json;
using MediatR;
using ModelContextProtocol.Server;
using Witness.Application.Commands;
using Witness.Application.Queries;

namespace Witness.McpServer.McpTools;

[McpServerToolType]
public sealed class WitnessTools
{
    [McpServerTool(Name = "witness_record")]
    [Description("Execute an HTTP request and capture the full interaction. Returns a WitnessId that can be used for replay and comparison.")]
    public static async Task<string> Record(
        IMediator mediator,
        [Description("Base URL of the target API (e.g., https://api.example.com)")] string target,
        [Description("HTTP method (GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS)")] string method,
        [Description("Request path (e.g., /api/loans)")] string path,
        [Description("HTTP headers as key-value pairs")] Dictionary<string, string>? headers = null,
        [Description("Request body (JSON string or omit for no body)")] string? body = null,
        [Description("Tag for this interaction (used in WitnessId)")] string? tag = null,
        [Description("Session ID to group related interactions")] string? sessionId = null,
        [Description("Human-readable description of what this interaction tests")] string? description = null,
        [Description("Request timeout in milliseconds (default: 30000)")] int? timeoutMs = null,
        [Description("Whether to follow HTTP redirects (default: true)")] bool? followRedirects = null)
    {
        var command = new RecordInteractionCommand
        {
            Target = target,
            Method = method,
            Path = path,
            Headers = headers,
            Body = body is not null ? JsonSerializer.Deserialize<object>(body) : null,
            Options = new RecordOptions
            {
                Tag = tag,
                SessionId = sessionId,
                Description = description,
                TimeoutMs = timeoutMs,
                FollowRedirects = followRedirects
            }
        };

        var result = await mediator.Send(command);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "witness_replay")]
    [Description("Replay a previously recorded interaction against a different target.")]
    public static async Task<string> Replay(
        IMediator mediator,
        [Description("The WitnessId of the interaction to replay")] string witnessId,
        [Description("New target URL to replay against")] string target,
        [Description("Tag for the replay interaction")] string? tag = null,
        [Description("Session ID for the replay")] string? sessionId = null,
        [Description("Headers to override in the replay as key-value pairs")] Dictionary<string, string>? overrideHeaders = null)
    {
        var command = new ReplayInteractionCommand
        {
            WitnessId = witnessId,
            Target = target,
            Options = new ReplayOptions
            {
                Tag = tag,
                SessionId = sessionId,
                OverrideHeaders = overrideHeaders
            }
        };

        var result = await mediator.Send(command);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "witness_inspect")]
    [Description("View the full details of a recorded interaction.")]
    public static async Task<string> Inspect(
        IMediator mediator,
        [Description("The WitnessId to inspect")] string witnessId,
        [Description("Optional session ID to narrow the search")] string? sessionId = null)
    {
        var query = new InspectInteractionQuery
        {
            WitnessId = witnessId,
            SessionId = sessionId
        };

        var result = await mediator.Send(query);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "witness_list")]
    [Description("List recorded sessions or interactions within a session.")]
    public static async Task<string> List(
        IMediator mediator,
        [Description("Optional session ID to list interactions from a specific session")] string? sessionId = null,
        [Description("Maximum number of results to return (default: 50)")] int limit = 50)
    {
        if (sessionId is not null)
        {
            var query = new ListInteractionsQuery
            {
                SessionId = sessionId,
                Limit = limit
            };
            var result = await mediator.Send(query);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        else
        {
            var query = new ListSessionsQuery
            {
                Limit = limit
            };
            var result = await mediator.Send(query);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
