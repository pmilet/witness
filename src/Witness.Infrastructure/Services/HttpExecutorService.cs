using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Witness.Domain.Services;
using Witness.Domain.ValueObjects;

namespace Witness.Infrastructure.Services;

public sealed class HttpExecutorService : IHttpExecutor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpExecutorService> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public HttpExecutorService(HttpClient httpClient, ILogger<HttpExecutorService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount} after {Delay}ms due to {Reason}",
                        retryCount, timespan.TotalMilliseconds, outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString());
                });
    }

    public async Task<HttpExecutionResult> ExecuteAsync(
        string target,
        string method,
        string path,
        IReadOnlyDictionary<string, string>? headers = null,
        object? body = null,
        HttpExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new HttpExecutionOptions();

        var url = BuildUrl(target, path);
        var request = CreateHttpRequestMessage(method, url, headers, body);

        _logger.LogInformation("Executing HTTP request: {Method} {Url}", method, url);

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(options.TimeoutMs);

            // Note: Redirect handling is managed by HttpClient configuration, not per-request
            response = await _retryPolicy.ExecuteAsync(async () => await _httpClient.SendAsync(request, cts.Token));
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogError("Request timed out after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            throw new TimeoutException($"HTTP request timed out after {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "HTTP request failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }

        stopwatch.Stop();
        var durationMs = stopwatch.ElapsedMilliseconds;

        var responseBody = await ReadResponseBodyAsync(response);
        var responseHeaders = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));
        if (response.Content.Headers != null)
        {
            foreach (var header in response.Content.Headers)
            {
                responseHeaders[header.Key] = string.Join(", ", header.Value);
            }
        }

        var httpRequest = new HttpRequest(
            method.ToUpperInvariant(),
            url,
            path,
            headers,
            body,
            request.Content?.Headers?.ContentType?.MediaType);

        var httpResponse = new HttpResponse(
            (int)response.StatusCode,
            responseHeaders,
            responseBody,
            response.Content.Headers?.ContentType?.MediaType,
            durationMs);

        _logger.LogInformation("Request completed: {StatusCode} in {DurationMs}ms", response.StatusCode, durationMs);

        return new HttpExecutionResult(httpRequest, httpResponse, durationMs);
    }

    private static string BuildUrl(string target, string path)
    {
        var baseUrl = target.TrimEnd('/');
        var normalizedPath = path.StartsWith('/') ? path : $"/{path}";
        return $"{baseUrl}{normalizedPath}";
    }

    private static HttpRequestMessage CreateHttpRequestMessage(
        string method,
        string url,
        IReadOnlyDictionary<string, string>? headers,
        object? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), url);

        // Add headers
        if (headers != null)
        {
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        // Add body for methods that support it
        if (body != null && (method.ToUpperInvariant() is "POST" or "PUT" or "PATCH"))
        {
            if (body is string strBody)
            {
                request.Content = new StringContent(strBody, Encoding.UTF8);
            }
            else
            {
                var json = JsonSerializer.Serialize(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
        }

        return request;
    }

    private static async Task<object?> ReadResponseBodyAsync(HttpResponseMessage response)
    {
        if (response.Content == null) return null;

        var contentType = response.Content.Headers?.ContentType?.MediaType;
        var content = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrEmpty(content)) return null;

        if (contentType?.Contains("json") == true)
        {
            try
            {
                return JsonSerializer.Deserialize<object>(content);
            }
            catch
            {
                return content;
            }
        }

        return content;
    }
}
