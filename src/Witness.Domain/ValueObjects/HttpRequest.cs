namespace Witness.Domain.ValueObjects;

/// <summary>
/// Represents an immutable HTTP request
/// </summary>
public sealed record HttpRequest
{
    public string Method { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    public object? Body { get; init; }
    public string? ContentType { get; init; }

    public HttpRequest() { }

    public HttpRequest(
        string method,
        string url,
        string path,
        IReadOnlyDictionary<string, string>? headers = null,
        object? body = null,
        string? contentType = null)
    {
        Method = method ?? throw new ArgumentNullException(nameof(method));
        Url = url ?? throw new ArgumentNullException(nameof(url));
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Headers = headers ?? new Dictionary<string, string>();
        Body = body;
        ContentType = contentType;
    }
}
