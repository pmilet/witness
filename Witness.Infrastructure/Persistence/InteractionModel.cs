namespace Witness.Infrastructure.Persistence;

public sealed class InteractionModel
{
    public string WitnessId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public HttpRequestModel Request { get; set; } = null!;
    public HttpResponseModel Response { get; set; } = null!;
    public InteractionMetadataModel Metadata { get; set; } = null!;
}

public sealed class HttpRequestModel
{
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public object? Body { get; set; }
    public string? ContentType { get; set; }
}

public sealed class HttpResponseModel
{
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public object? Body { get; set; }
    public string? ContentType { get; set; }
    public long DurationMs { get; set; }
}

public sealed class InteractionMetadataModel
{
    public List<string> Tags { get; set; } = new();
    public string? Description { get; set; }
    public string? OpenApiOperationId { get; set; }
    public int? ChainStep { get; set; }
    public string? ChainId { get; set; }
}
