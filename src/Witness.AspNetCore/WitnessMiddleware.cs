using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Witness.Domain.Entities;
using Witness.Domain.ValueObjects;
using Witness.Infrastructure.Configuration;
using Witness.Infrastructure.Repositories;

namespace Witness.AspNetCore;

/// <summary>
/// ASP.NET middleware that enables Witness record/replay for inbound requests.
///
/// When an inbound request carries the header X-Witness-Mode: record, the middleware
/// sets up an AsyncLocal correlation context. All outbound HttpClient calls made
/// through WitnessCaptureHandler are collected in this context. After the response
/// is produced, the middleware stores the complete interaction (inbound + outbound
/// calls) to the witness-store and returns the WitnessId in a response header.
///
/// When an inbound request carries X-Witness-Mode: replay and X-Witness-Id: {id},
/// the middleware loads the previously recorded interaction (with its OutboundCalls),
/// populates the replay playback queue, and outbound calls are served from recorded
/// data without making real HTTP requests.
/// </summary>
public sealed class WitnessMiddleware
{
    private readonly RequestDelegate _next;
    private readonly FileSystemInteractionRepository _repository;
    private readonly ILogger<WitnessMiddleware> _logger;

    public WitnessMiddleware(RequestDelegate next, WitnessMiddlewareOptions options, ILoggerFactory loggerFactory)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _repository = BuildRepository(options.StorePath);
        _logger = loggerFactory.CreateLogger<WitnessMiddleware>();
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        var modeHeader = httpContext.Request.Headers["X-Witness-Mode"].FirstOrDefault();

        if (string.Equals(modeHeader, "record", StringComparison.OrdinalIgnoreCase))
        {
            await HandleRecord(httpContext);
        }
        else if (string.Equals(modeHeader, "replay", StringComparison.OrdinalIgnoreCase))
        {
            await HandleReplay(httpContext);
        }
        else
        {
            await _next(httpContext);
        }
    }

    private async Task HandleRecord(HttpContext httpContext)
    {
        var correlationId = httpContext.Request.Headers["X-Witness-Id"].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N")[..12];

        _logger.LogInformation("Witness record mode — correlation: {CorrelationId}", correlationId);

        // Set up correlation context so WitnessCaptureHandler collects outbound calls
        WitnessCallContext.Current = WitnessCallContext.CreateForRecord(correlationId);

        // Capture the inbound request
        var inboundRequest = await CaptureInboundRequest(httpContext);

        // Buffer the response so we can capture it
        var originalBodyStream = httpContext.Response.Body;
        using var responseBuffer = new MemoryStream();
        httpContext.Response.Body = responseBuffer;

        var stopwatch = Stopwatch.StartNew();
        await _next(httpContext);
        stopwatch.Stop();

        // Read captured response
        responseBuffer.Seek(0, SeekOrigin.Begin);
        var responseBodyText = await new StreamReader(responseBuffer).ReadToEndAsync();

        // Build the inbound interaction with outbound calls attached
        var ctx = WitnessCallContext.Current;
        var outboundCalls = ctx?.CapturedOutbound.ToList();

        object? responseBody = TryParseJson(responseBodyText);
        var responseHeaders = httpContext.Response.Headers
            .Where(h => h.Value.Count > 0)
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value.ToArray()));

        var domainResponse = new Domain.ValueObjects.HttpResponse(
            httpContext.Response.StatusCode,
            responseHeaders.AsReadOnly(),
            responseBody,
            httpContext.Response.ContentType,
            stopwatch.ElapsedMilliseconds);

        var sessionId = httpContext.Request.Headers["X-Witness-Session"].FirstOrDefault()
                        ?? $"session-{DateTime.UtcNow:yyyy-MM-dd}";
        var tag = httpContext.Request.Headers["X-Witness-Tag"].FirstOrDefault() ?? "inbound";

        var witnessId = WitnessId.Generate(tag, inboundRequest.Method, inboundRequest.Path, inboundRequest.Body);

        var metadata = new InteractionMetadata(tags: new[] { tag });
        var interaction = Interaction.Create(
            witnessId, sessionId, inboundRequest, domainResponse, metadata, outboundCalls);

        await _repository.SaveAsync(interaction, CancellationToken.None);

        // Return WitnessId to the caller
        httpContext.Response.Headers["X-Witness-Id"] = witnessId.Value;

        _logger.LogInformation("Witness recorded: {WitnessId} with {OutboundCount} outbound call(s)",
            witnessId.Value, outboundCalls?.Count ?? 0);

        // Write the buffered response to the original stream
        responseBuffer.Seek(0, SeekOrigin.Begin);
        await responseBuffer.CopyToAsync(originalBodyStream);
        httpContext.Response.Body = originalBodyStream;

        // Clear context
        WitnessCallContext.Current = null;
    }

    private async Task HandleReplay(HttpContext httpContext)
    {
        var witnessIdHeader = httpContext.Request.Headers["X-Witness-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(witnessIdHeader))
        {
            httpContext.Response.StatusCode = 400;
            await httpContext.Response.WriteAsync("X-Witness-Id header required for replay mode");
            return;
        }

        _logger.LogInformation("Witness replay mode — loading: {WitnessId}", witnessIdHeader);

        var witnessId = WitnessId.Parse(witnessIdHeader);
        var sessionId = httpContext.Request.Headers["X-Witness-Session"].FirstOrDefault();
        var recorded = await _repository.GetByIdAsync(witnessId, sessionId, CancellationToken.None);

        if (recorded == null)
        {
            httpContext.Response.StatusCode = 404;
            await httpContext.Response.WriteAsync($"Recorded interaction not found: {witnessIdHeader}");
            return;
        }

        var outboundCalls = recorded.OutboundCalls ?? Array.Empty<Interaction>();
        _logger.LogInformation("Witness replay: {OutboundCount} outbound call(s) queued for playback",
            outboundCalls.Count);

        // Set up replay context so WitnessCaptureHandler serves recorded responses
        WitnessCallContext.Current = WitnessCallContext.CreateForReplay(witnessIdHeader, outboundCalls);

        await _next(httpContext);

        // Clear context
        WitnessCallContext.Current = null;
    }

    private static async Task<Domain.ValueObjects.HttpRequest> CaptureInboundRequest(HttpContext httpContext)
    {
        httpContext.Request.EnableBuffering();
        string? bodyText = null;
        if (httpContext.Request.ContentLength > 0 || httpContext.Request.ContentType is not null)
        {
            using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
            bodyText = await reader.ReadToEndAsync();
            httpContext.Request.Body.Position = 0;
        }

        var method = httpContext.Request.Method.ToUpperInvariant();
        var path = httpContext.Request.Path + httpContext.Request.QueryString;
        var url = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{path}";

        var headers = httpContext.Request.Headers
            .Where(h => h.Value.Count > 0)
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value.ToArray()));

        object? body = TryParseJson(bodyText);

        return new Domain.ValueObjects.HttpRequest(method, url, path, headers.AsReadOnly(), body, httpContext.Request.ContentType);
    }

    private static object? TryParseJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return JsonSerializer.Deserialize<JsonElement>(text); }
        catch { return text; }
    }

    private static FileSystemInteractionRepository BuildRepository(string storePath)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(new WitnessOptions
        {
            Storage = new StorageOptions { Path = storePath }
        });
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemInteractionRepository>.Instance;
        return new FileSystemInteractionRepository(opts, logger);
    }
}
