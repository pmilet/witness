namespace Witness.Application.DTOs;

/// <summary>
/// DTO for listing sessions and interactions
/// </summary>
public sealed record ListSessionsResponseDto
{
    public required List<SessionSummaryDto> Sessions { get; init; }
    public required int TotalCount { get; init; }
}

public sealed record SessionSummaryDto
{
    public required string SessionId { get; init; }
    public required string CreatedAt { get; init; }
    public string? Description { get; init; }
    public List<string>? Tags { get; init; }
    public required int InteractionCount { get; init; }
}

public sealed record ListInteractionsRequestDto
{
    public required string SessionId { get; init; }
}

public sealed record ListInteractionsResponseDto
{
    public required string SessionId { get; init; }
    public required List<InteractionSummaryDto> Interactions { get; init; }
    public required int TotalCount { get; init; }
}

public sealed record InteractionSummaryDto
{
    public required string WitnessId { get; init; }
    public required string Timestamp { get; init; }
    public required string Method { get; init; }
    public required string Path { get; init; }
    public required int StatusCode { get; init; }
    public required long DurationMs { get; init; }
}
