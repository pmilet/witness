using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Witness.Domain.Entities;
using Witness.Domain.ValueObjects;
using Witness.Infrastructure.Configuration;
using Witness.Infrastructure.Repositories;
using Witness.Proxy;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

builder.Services.Configure<ProxyOptions>(
    builder.Configuration.GetSection(ProxyOptions.SectionName));

builder.WebHost.ConfigureKestrel((ctx, opts) =>
{
    var port = ctx.Configuration.GetSection(ProxyOptions.SectionName).GetValue<int>("Port", 9999);
    opts.ListenAnyIP(port);
});

// Silence ASP.NET Core startup banner on stdout so it doesn't pollute test output
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", Microsoft.Extensions.Logging.LogLevel.None);

var app = builder.Build();

var proxyOptions = app.Services.GetRequiredService<IOptions<ProxyOptions>>().Value;

if (string.IsNullOrWhiteSpace(proxyOptions.Upstream))
{
    Console.Error.WriteLine("Witness.Proxy: Proxy:Upstream must be set.");
    return 1;
}

var upstream = proxyOptions.Upstream.TrimEnd('/');
var repository = BuildRepository(proxyOptions.StorePath);
var httpClient = new HttpClient();

app.Use(async (HttpContext context, RequestDelegate next) =>
{
    var stopwatch = Stopwatch.StartNew();

    var path = context.Request.Path.Value ?? "/";
    var query = context.Request.QueryString.Value ?? "";
    var targetUri = new Uri(upstream + path + query);
    var method = context.Request.Method.ToUpperInvariant();

    // ── Read request body ───────────────────────────────────────────────
    string requestBodyText = "";
    if (context.Request.ContentLength > 0 ||
        string.Equals(context.Request.Headers.TransferEncoding, "chunked", StringComparison.OrdinalIgnoreCase))
    {
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        requestBodyText = await reader.ReadToEndAsync(context.RequestAborted);
    }

    // ── Build and send forward request ─────────────────────────────────
    using var forwardRequest = new HttpRequestMessage(new HttpMethod(method), targetUri);

    foreach (var header in context.Request.Headers)
    {
        if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
        if (!forwardRequest.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string?>)header.Value.ToArray()))
            forwardRequest.Content?.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string?>)header.Value.ToArray());
    }

    if (!string.IsNullOrEmpty(requestBodyText))
    {
        // Extract bare media type — StringContent ctor rejects "type; param=value" form
        var mediaType = (context.Request.ContentType ?? "application/octet-stream")
            .Split(';')[0].Trim();
        forwardRequest.Content = new StringContent(requestBodyText, Encoding.UTF8, mediaType);
    }

    HttpResponseMessage forwardResponse;
    try
    {
        forwardResponse = await httpClient.SendAsync(forwardRequest, context.RequestAborted);
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 502;
        await context.Response.WriteAsync($"Proxy error: {ex.Message}");
        return;
    }

    stopwatch.Stop();

    // ── Read response body ──────────────────────────────────────────────
    var responseBytes = await forwardResponse.Content.ReadAsByteArrayAsync(context.RequestAborted);
    var responseBodyText = Encoding.UTF8.GetString(responseBytes);

    // ── Record interaction ──────────────────────────────────────────────
    object? requestBody = TryParseJson(requestBodyText);
    object? responseBody = TryParseJson(responseBodyText);

    var requestHeaders = context.Request.Headers
        .ToDictionary(h => h.Key, h => h.Value.ToString());

    var responseHeaders = forwardResponse.Headers
        .Concat(forwardResponse.Content.Headers)
        .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

    var domainRequest = new Witness.Domain.ValueObjects.HttpRequest(
        method, targetUri.ToString(), path + query,
        requestHeaders.AsReadOnly(),
        requestBody,
        context.Request.ContentType);

    var domainResponse = new Witness.Domain.ValueObjects.HttpResponse(
        (int)forwardResponse.StatusCode,
        responseHeaders.AsReadOnly(),
        responseBody,
        forwardResponse.Content.Headers.ContentType?.ToString(),
        stopwatch.ElapsedMilliseconds);

    var witnessId = WitnessId.Generate(proxyOptions.Tag, method, path + query, requestBody);
    var metadata = new InteractionMetadata(tags: new[] { proxyOptions.Tag });
    var interaction = Interaction.Create(witnessId, proxyOptions.SessionId, domainRequest, domainResponse, metadata);

    await repository.SaveAsync(interaction, context.RequestAborted);

    // ── Write response back to caller ───────────────────────────────────
    context.Response.StatusCode = (int)forwardResponse.StatusCode;

    foreach (var header in forwardResponse.Headers.Concat(forwardResponse.Content.Headers))
    {
        if (header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) continue;
        context.Response.Headers.TryAdd(header.Key, string.Join(", ", header.Value));
    }

    context.Response.ContentType = forwardResponse.Content.Headers.ContentType?.ToString()
        ?? "application/octet-stream";

    await context.Response.Body.WriteAsync(responseBytes, context.RequestAborted);
});

await app.RunAsync();
return 0;

static object? TryParseJson(string? text)
{
    if (string.IsNullOrWhiteSpace(text)) return null;
    try { return JsonSerializer.Deserialize<JsonElement>(text); }
    catch { return text; }
}

static FileSystemInteractionRepository BuildRepository(string storePath)
{
    var opts = Options.Create(new WitnessOptions
    {
        Storage = new StorageOptions { Path = storePath }
    });
    var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemInteractionRepository>.Instance;
    return new FileSystemInteractionRepository(opts, logger);
}
