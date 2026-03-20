using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Witness.Domain.Entities;
using Witness.Domain.ValueObjects;
using Witness.Infrastructure.Configuration;
using Witness.Infrastructure.Repositories;

namespace Witness.AspNetCore;

/// <summary>
/// A DelegatingHandler that transparently captures every outbound HttpClient call
/// and saves it to the witness-store as a recorded interaction.
///
/// Usage (manual):
///   var handler = new WitnessCaptureHandler(options, new HttpClientHandler());
///   var client  = new HttpClient(handler) { BaseAddress = ... };
///
/// Usage (DI / IHttpClientBuilder):
///   services.AddHttpClient("my-client")
///           .AddWitnessCapture(opt => { opt.SessionId = "my-session"; });
/// </summary>
public sealed class WitnessCaptureHandler : DelegatingHandler
{
    private readonly WitnessCaptureOptions _options;
    private readonly FileSystemInteractionRepository _repository;

    public WitnessCaptureHandler(WitnessCaptureOptions options, HttpMessageHandler? innerHandler = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _repository = BuildRepository(options.StorePath);

        if (innerHandler != null)
            InnerHandler = innerHandler;
    }

    // Constructor for DI — options resolved from IOptions<WitnessCaptureOptions>
    public WitnessCaptureHandler(IOptions<WitnessCaptureOptions> options)
        : this(options.Value) { }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        // ── Snapshot request body before forwarding ────────────────────────
        string? requestBodyText = null;
        if (request.Content is not null)
        {
            requestBodyText = await request.Content.ReadAsStringAsync(cancellationToken);
            // Recreate so the inner handler can still read it
            var bytes = Encoding.UTF8.GetBytes(requestBodyText);
            var contentType = request.Content.Headers.ContentType;
            request.Content = new ByteArrayContent(bytes);
            if (contentType is not null)
                request.Content.Headers.ContentType = contentType;
        }

        // ── Forward ────────────────────────────────────────────────────────
        var response = await base.SendAsync(request, cancellationToken);
        stopwatch.Stop();

        // ── Snapshot response body ──────────────────────────────────────────
        var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var responseBodyText = Encoding.UTF8.GetString(responseBytes);

        // Recreate so the caller can still read the response body
        var responseContentType = response.Content.Headers.ContentType;
        response.Content = new ByteArrayContent(responseBytes);
        if (responseContentType is not null)
            response.Content.Headers.ContentType = responseContentType;

        // ── Build domain objects ────────────────────────────────────────────
        var uri = request.RequestUri!;
        var path = uri.PathAndQuery;
        var url = uri.ToString();
        var method = request.Method.Method.ToUpperInvariant();

        object? requestBody = TryParseJson(requestBodyText);
        object? responseBody = TryParseJson(responseBodyText);

        var requestHeaders = request.Headers
            .Where(h => h.Value.Any())
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

        var responseHeaders = response.Headers
            .Concat(response.Content.Headers)
            .Where(h => h.Value.Any())
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

        var domainRequest = new Witness.Domain.ValueObjects.HttpRequest(
            method, url, path,
            requestHeaders.AsReadOnly(),
            requestBody,
            request.Content?.Headers.ContentType?.ToString());

        var domainResponse = new Witness.Domain.ValueObjects.HttpResponse(
            (int)response.StatusCode,
            responseHeaders.AsReadOnly(),
            responseBody,
            response.Content.Headers.ContentType?.ToString(),
            stopwatch.ElapsedMilliseconds);

        var witnessId = WitnessId.Generate(_options.Tag, method, path, requestBody);
        var metadata = new InteractionMetadata(tags: new[] { _options.Tag });
        var interaction = Interaction.Create(witnessId, _options.SessionId, domainRequest, domainResponse, metadata);

        await _repository.SaveAsync(interaction, cancellationToken);

        return response;
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
