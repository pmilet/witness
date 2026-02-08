namespace Witness.Domain.ValueObjects;

/// <summary>
/// Represents an HTTP response
/// </summary>
public sealed record HttpResponse
{
    public int StatusCode { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; }
    public string? Body { get; init; }
    public string? ContentType { get; init; }
    public long DurationMs { get; init; }

    private HttpResponse(
        int statusCode,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        string? contentType,
        long durationMs)
    {
        StatusCode = statusCode;
        Headers = headers;
        Body = body;
        ContentType = contentType;
        DurationMs = durationMs;
    }

    public static HttpResponse Create(
        int statusCode,
        Dictionary<string, string>? headers = null,
        string? body = null,
        string? contentType = null,
        long durationMs = 0)
    {
        if (statusCode < 100 || statusCode > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode), $"Status code must be between 100 and 599, got {statusCode}");
        }

        return new HttpResponse(
            statusCode,
            headers?.AsReadOnly() ?? new Dictionary<string, string>().AsReadOnly(),
            body,
            contentType,
            durationMs);
    }
}
