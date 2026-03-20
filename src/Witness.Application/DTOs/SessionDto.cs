namespace Witness.Application.DTOs;

public sealed record SessionDto
{
    public string SessionId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public List<string> Tags { get; init; } = new();
    public int InteractionCount { get; init; }
    public string? Description { get; init; }
}
