namespace Witness.Application.DTOs;

public sealed record InteractionDto
{
    public string WitnessId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public HttpRequestDto Request { get; init; } = null!;
    public HttpResponseDto Response { get; init; } = null!;
    public InteractionMetadataDto Metadata { get; init; } = null!;
}

public sealed record HttpRequestDto
{
    public string Method { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public Dictionary<string, string> Headers { get; init; } = new();
    public object? Body { get; init; }
    public string? ContentType { get; init; }
}

public sealed record HttpResponseDto
{
    public int StatusCode { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new();
    public object? Body { get; init; }
    public string? ContentType { get; init; }
    public long DurationMs { get; init; }
}

public sealed record InteractionMetadataDto
{
    public List<string> Tags { get; init; } = new();
    public string? Description { get; init; }
    public string? OpenApiOperationId { get; init; }
    public int? ChainStep { get; init; }
    public string? ChainId { get; init; }
}
