namespace Witness.Application.DTOs;

/// <summary>
/// DTO for inspecting an interaction
/// </summary>
public sealed record InspectRequestDto
{
    public required string WitnessId { get; init; }
    public required string SessionId { get; init; }
}

public sealed record InteractionDto
{
    public required string WitnessId { get; init; }
    public required string SessionId { get; init; }
    public required string Timestamp { get; init; }
    public required RequestDto Request { get; init; }
    public required ResponseDto Response { get; init; }
    public required MetadataDto Metadata { get; init; }
}

public sealed record RequestDto
{
    public required string Method { get; init; }
    public required string Url { get; init; }
    public required string Path { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string? Body { get; init; }
    public string? ContentType { get; init; }
}

public sealed record ResponseDto
{
    public required int StatusCode { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string? Body { get; init; }
    public string? ContentType { get; init; }
    public required long DurationMs { get; init; }
}

public sealed record MetadataDto
{
    public List<string>? Tags { get; init; }
    public string? Description { get; init; }
    public string? OpenApiOperationId { get; init; }
    public int? ChainStep { get; init; }
    public string? ChainId { get; init; }
}
