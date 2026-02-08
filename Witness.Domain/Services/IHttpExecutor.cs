using Witness.Domain.ValueObjects;

namespace Witness.Domain.Services;

/// <summary>
/// Domain service interface for executing HTTP requests
/// </summary>
public interface IHttpExecutor
{
    /// <summary>
    /// Execute an HTTP request and return the response
    /// </summary>
    Task<HttpExecutionResult> ExecuteAsync(
        string target,
        string method,
        string path,
        IReadOnlyDictionary<string, string>? headers = null,
        object? body = null,
        HttpExecutionOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for HTTP execution
/// </summary>
public sealed class HttpExecutionOptions
{
    public int TimeoutMs { get; init; } = 30000;
    public bool FollowRedirects { get; init; } = true;
    public int MaxRedirects { get; init; } = 5;
}

/// <summary>
/// Result of HTTP execution
/// </summary>
public sealed class HttpExecutionResult
{
    public HttpRequest Request { get; init; }
    public HttpResponse Response { get; init; }
    public long DurationMs { get; init; }

    public HttpExecutionResult(HttpRequest request, HttpResponse response, long durationMs)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Response = response ?? throw new ArgumentNullException(nameof(response));
        DurationMs = durationMs;
    }
}
