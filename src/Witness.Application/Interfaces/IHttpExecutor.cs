using Witness.Domain.ValueObjects;

namespace Witness.Application.Interfaces;

/// <summary>
/// Interface for executing HTTP requests
/// </summary>
public interface IHttpExecutor
{
    /// <summary>
    /// Executes an HTTP request and returns the response
    /// </summary>
    Task<HttpResponse> ExecuteAsync(
        HttpRequest request,
        bool followRedirects = true,
        int timeoutMs = 30000,
        CancellationToken cancellationToken = default);
}
