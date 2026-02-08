namespace Witness.Domain.ValueObjects;

/// <summary>
/// Represents an HTTP request
/// </summary>
public sealed record HttpRequest
{
    public HttpMethod Method { get; init; }
    public string Url { get; init; }
    public string Path { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; }
    public string? Body { get; init; }
    public string? ContentType { get; init; }

    private HttpRequest(
        HttpMethod method,
        string url,
        string path,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        string? contentType)
    {
        Method = method;
        Url = url;
        Path = path;
        Headers = headers;
        Body = body;
        ContentType = contentType;
    }

    public static HttpRequest Create(
        HttpMethod method,
        string url,
        string path,
        Dictionary<string, string>? headers = null,
        string? body = null,
        string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(method, nameof(method));
        ArgumentException.ThrowIfNullOrWhiteSpace(url, nameof(url));
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));

        return new HttpRequest(
            method,
            url,
            path,
            headers?.AsReadOnly() ?? new Dictionary<string, string>().AsReadOnly(),
            body,
            contentType);
    }
}
