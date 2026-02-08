using System.Diagnostics;
using Witness.Application.Interfaces;
using Witness.Domain.ValueObjects;

namespace Witness.Infrastructure.Http;

/// <summary>
/// HTTP executor implementation using HttpClient
/// </summary>
public sealed class HttpExecutor : IHttpExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpExecutor(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public async Task<HttpResponse> ExecuteAsync(
        HttpRequest request,
        bool followRedirects = true,
        int timeoutMs = 30000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var client = _httpClientFactory.CreateClient("WitnessHttpClient");
        client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);

        var httpRequestMessage = new HttpRequestMessage
        {
            Method = new System.Net.Http.HttpMethod(request.Method.Value),
            RequestUri = new Uri(request.Url)
        };

        foreach (var (key, value) in request.Headers)
        {
            httpRequestMessage.Headers.TryAddWithoutValidation(key, value);
        }

        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            httpRequestMessage.Content = new StringContent(
                request.Body,
                System.Text.Encoding.UTF8,
                request.ContentType ?? "application/json");
        }

        var stopwatch = Stopwatch.StartNew();
        var httpResponseMessage = await client.SendAsync(httpRequestMessage, cancellationToken);
        stopwatch.Stop();

        var responseHeaders = new Dictionary<string, string>();
        foreach (var header in httpResponseMessage.Headers)
        {
            responseHeaders[header.Key] = string.Join(", ", header.Value);
        }

        if (httpResponseMessage.Content?.Headers != null)
        {
            foreach (var header in httpResponseMessage.Content.Headers)
            {
                responseHeaders[header.Key] = string.Join(", ", header.Value);
            }
        }

        var responseBody = httpResponseMessage.Content != null 
            ? await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken)
            : string.Empty;
        var contentType = httpResponseMessage.Content?.Headers?.ContentType?.MediaType ?? string.Empty;

        return HttpResponse.Create(
            (int)httpResponseMessage.StatusCode,
            responseHeaders,
            responseBody,
            contentType,
            stopwatch.ElapsedMilliseconds);
    }
}
