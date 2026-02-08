namespace Witness.Domain.ValueObjects;

/// <summary>
/// Represents an HTTP method as a value object
/// </summary>
public sealed record HttpMethod
{
    public string Value { get; init; }

    private HttpMethod(string value)
    {
        Value = value;
    }

    public static HttpMethod Get => new("GET");
    public static HttpMethod Post => new("POST");
    public static HttpMethod Put => new("PUT");
    public static HttpMethod Delete => new("DELETE");
    public static HttpMethod Patch => new("PATCH");

    public static HttpMethod FromString(string method)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method, nameof(method));

        return method.ToUpperInvariant() switch
        {
            "GET" => Get,
            "POST" => Post,
            "PUT" => Put,
            "DELETE" => Delete,
            "PATCH" => Patch,
            _ => new HttpMethod(method.ToUpperInvariant())
        };
    }

    public override string ToString() => Value;
}
