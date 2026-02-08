namespace Witness.Domain.ValueObjects;

/// <summary>
/// Represents an immutable HTTP response
/// </summary>
public sealed record HttpResponse
{
    public int StatusCode { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    public object? Body { get; init; }
    public string? ContentType { get; init; }
    public long DurationMs { get; init; }

    public HttpResponse() { }

    public HttpResponse(
        int statusCode,
        IReadOnlyDictionary<string, string>? headers = null,
        object? body = null,
        string? contentType = null,
        long durationMs = 0)
    {
        if (statusCode < 100 || statusCode >= 600)
            throw new ArgumentOutOfRangeException(nameof(statusCode), "Status code must be between 100 and 599");

        StatusCode = statusCode;
        Headers = headers ?? new Dictionary<string, string>();
        Body = body;
        ContentType = contentType;
        DurationMs = durationMs;
    }
}
